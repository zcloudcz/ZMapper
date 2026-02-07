using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZMapper.SourceGenerator;

/// <summary>
/// ZMapper Source Generator - Generates optimized mapping code at compile-time.
///
/// This is a Roslyn Incremental Source Generator that:
/// 1. Finds methods that configure mappings (using CreateMap)
/// 2. Finds classes implementing IMapperProfile (profile-based configuration)
/// 3. Analyzes the mapping configuration (ForMember, Ignore, etc.)
/// 4. Generates optimized C# code for performing the mappings
/// 5. Generates DI extension (AddZMapper) when M.E.DI is referenced
///
/// For beginners: A source generator is like a "code writer" that runs during compilation.
/// It looks at your code, understands what you want, and writes new code for you automatically!
/// </summary>
[Generator] // This attribute tells Roslyn this is a source generator
public class ZMapperGenerator : IIncrementalGenerator
{
    // Diagnostic emitted when a destination property has no matching source property
    // and IgnoreNonExisting() was NOT called on the mapping.
    private static readonly DiagnosticDescriptor UnmappedPropertyDiagnostic = new(
        id: "ZMAP001",
        title: "Unmapped destination property",
        messageFormat: "Destination property '{0}' on type '{1}' has no matching source property on '{2}'. " +
                       "Use .ForMember(d => d.{0}, opt => opt.Ignore()) to explicitly ignore, " +
                       "or .IgnoreNonExisting() to skip all non-matching properties.",
        category: "ZMapper",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Initialize is called by Roslyn when compilation starts.
    /// This is where we register what syntax we're interested in finding.
    ///
    /// Think of this as: "Hey Roslyn, tell me whenever you see methods with CreateMap calls!"
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // PROVIDER 1: Detect methods containing CreateMap calls (existing pattern)
        // This finds classes like: partial class MapperConfig { void ConfigureMapper() { config.CreateMap<A,B>(); } }
        var mapperConfigurations = context.SyntaxProvider
            .CreateSyntaxProvider(
                // PREDICATE: Quick check - is this syntax node potentially interesting?
                // This runs FAST on every syntax node, so we keep it simple
                predicate: static (s, _) => IsPotentialMapperMethod(s),

                // TRANSFORM: For interesting nodes, extract the detailed information we need
                // This runs SLOWER but only on nodes that passed the predicate
                transform: static (ctx, _) => GetMapperConfigurationIfValid(ctx))

            // Filter out nulls (nodes that weren't actually mapper configs)
            .Where(static m => m is not null)

            // Collect all the configurations into a single array
            .Collect();

        // PROVIDER 2: Detect classes implementing IMapperProfile (new profile pattern)
        // This finds classes like: public class UserProfile : IMapperProfile { void Configure(...) { } }
        var profileConfigurations = context.SyntaxProvider
            .CreateSyntaxProvider(
                // PREDICATE: Quick check - does this class implement IMapperProfile?
                predicate: static (s, _) => IsPotentialProfileClass(s),

                // TRANSFORM: Extract mapping information from the profile's Configure method
                transform: static (ctx, _) => GetProfileConfigurationIfValid(ctx))

            // Filter out nulls
            .Where(static m => m is not null)

            // Collect all profile configurations
            .Collect();

        // Combine both providers with the compilation (needed to check referenced assemblies)
        var combined = mapperConfigurations
            .Combine(profileConfigurations)
            .Combine(context.CompilationProvider);

        // Step 2: Register our code generation function
        // When compilation needs our generated code, call Execute()
        context.RegisterSourceOutput(combined,
            static (spc, source) =>
            {
                var ((mapperModels, profileModels), compilation) = source;
                Execute(mapperModels!, profileModels!, compilation, spc);
            });
    }

    // ============================================================================
    // PROVIDER 1: CreateMap-based detection (existing pattern)
    // ============================================================================

    /// <summary>
    /// Quick check: Could this syntax node be a mapper configuration method?
    ///
    /// We're looking for:
    /// - A method declaration (void ConfigureMapper() { ... })
    /// - That has a body (not abstract or interface method)
    /// - Contains at least one call to "CreateMap"
    ///
    /// For beginners: This is like skimming a book to find chapters about cooking.
    /// You don't read every word, just check if the chapter mentions "recipe".
    /// </summary>
    private static bool IsPotentialMapperMethod(SyntaxNode node)
    {
        // Is this a method? (not a class, property, etc.)
        return node is MethodDeclarationSyntax method &&

               // Does it have a body? (methods with bodies have { ... })
               method.Body != null &&

               // Does the body contain any "CreateMap" calls?
               method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                   .Any(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                              mae.Name.Identifier.Text == "CreateMap");
    }

    /// <summary>
    /// Extract detailed information from a method that configures mappings.
    ///
    /// This analyzes:
    /// - Which class contains the method
    /// - All CreateMap calls in the method
    /// - For each CreateMap: source type, destination type, and configuration (ForMember, etc.)
    ///
    /// For beginners: Now we're reading the recipe in detail - what ingredients (types)
    /// do we need, and what are the steps (mappings)?
    /// </summary>
    private static MapperConfigurationModel? GetMapperConfigurationIfValid(
        GeneratorSyntaxContext context)
    {
        // Get the method we're analyzing
        var method = (MethodDeclarationSyntax)context.Node;

        // Find the class that contains this method
        // We need to know which class to make "partial" and add our generated code to
        var containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass == null) return null; // Method not in a class? Skip it.

        // Skip classes that implement IMapperProfile - those are handled by Provider 2
        if (ImplementsIMapperProfile(containingClass, context.SemanticModel))
            return null;

        // Find ALL CreateMap calls in this method
        // Example: config.CreateMap<UserDto, User>()...
        var createMapCalls = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>() // Find all method calls
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                         mae.Name.Identifier.Text == "CreateMap") // That are named "CreateMap"
            .ToList();

        // No CreateMap calls? This isn't a mapper config method after all.
        if (createMapCalls.Count == 0) return null;

        // Process each CreateMap call to extract mapping configuration
        var mappings = new List<MappingModel>();
        foreach (var call in createMapCalls)
        {
            var mapping = ExtractMappingFromCall(call, context.SemanticModel);
            if (mapping != null)
            {
                mappings.Add(mapping);

                // Check if this mapping has ReverseMap() call
                var reverseMapping = ExtractReverseMappingIfPresent(call, mapping, context.SemanticModel);
                if (reverseMapping != null)
                {
                    mappings.Add(reverseMapping);
                }
            }
        }

        // If we couldn't extract any valid mappings, return null
        if (mappings.Count == 0) return null;

        // Get the namespace of the class (e.g., "MyApp.Mappers")
        var namespaceName = containingClass.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString();

        // Get the class name (e.g., "MapperConfig")
        var className = containingClass.Identifier.Text;

        // Return all the information we collected
        // Check if method takes a MapperConfiguration parameter
        bool methodTakesConfig = false;
        string debugTypeInfo = $"paramCount={method.ParameterList.Parameters.Count}";
        if (method.ParameterList.Parameters.Count == 1)
        {
            var paramType = method.ParameterList.Parameters[0].Type;
            if (paramType != null)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(paramType);
                if (typeInfo.Type != null)
                {
                    var fullTypeName = typeInfo.Type.ToDisplayString();
                    var typeName = typeInfo.Type.Name;
                    debugTypeInfo = $"{typeName}|{fullTypeName}";
                    // Check both full name and simple name
                    methodTakesConfig = fullTypeName.Contains("MapperConfiguration") ||
                                      typeName == "MapperConfiguration";
                }
                else
                {
                    debugTypeInfo = "type-null";
                }
            }
            else
            {
                debugTypeInfo = "paramType-null";
            }
        }

        return new MapperConfigurationModel
        {
            Namespace = namespaceName ?? "ZMapper.Generated", // Use default if no namespace
            ClassName = className,
            MethodName = method.Identifier.Text,
            MethodTakesConfig = methodTakesConfig,
            DebugParamInfo = debugTypeInfo,
            Mappings = mappings
        };
    }

    // ============================================================================
    // PROVIDER 2: IMapperProfile-based detection (new profile pattern)
    // ============================================================================

    /// <summary>
    /// Quick check: Could this syntax node be a class implementing IMapperProfile?
    ///
    /// We check if a class declaration has a base list that mentions "IMapperProfile".
    ///
    /// For beginners: This scans for classes that say "I'm a mapping profile!"
    /// by looking at their inheritance list (the part after the colon).
    /// </summary>
    private static bool IsPotentialProfileClass(SyntaxNode node)
    {
        // Must be a class declaration
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        // Must have a base list (: IMapperProfile or : SomeBase, IMapperProfile)
        if (classDecl.BaseList == null)
            return false;

        // Check if any base type mentions "IMapperProfile"
        return classDecl.BaseList.Types.Any(t =>
            t.Type.ToString().Contains("IMapperProfile"));
    }

    /// <summary>
    /// Extract mapping configuration from a class implementing IMapperProfile.
    ///
    /// This finds the Configure(MapperConfiguration config) method and extracts
    /// all CreateMap calls from it - reusing the same extraction logic as Provider 1.
    ///
    /// For beginners: A profile is a neat way to organize mappings.
    /// Instead of one giant config class, you create small focused profile classes.
    /// </summary>
    private static ProfileConfigurationModel? GetProfileConfigurationIfValid(
        GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        // Verify using semantic model that this class actually implements IMapperProfile
        if (!ImplementsIMapperProfile(classDecl, context.SemanticModel))
            return null;

        // Find the Configure method
        var configureMethod = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Configure" && m.Body != null);

        if (configureMethod == null)
            return null;

        // Extract all CreateMap calls from the Configure method
        var createMapCalls = configureMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                         mae.Name.Identifier.Text == "CreateMap")
            .ToList();

        if (createMapCalls.Count == 0) return null;

        // Process each CreateMap call (reuses same logic as Provider 1)
        var mappings = new List<MappingModel>();
        foreach (var call in createMapCalls)
        {
            var mapping = ExtractMappingFromCall(call, context.SemanticModel);
            if (mapping != null)
            {
                mappings.Add(mapping);

                var reverseMapping = ExtractReverseMappingIfPresent(call, mapping, context.SemanticModel);
                if (reverseMapping != null)
                {
                    mappings.Add(reverseMapping);
                }
            }
        }

        if (mappings.Count == 0) return null;

        // Get namespace and class name
        var namespaceName = classDecl.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString();
        var className = classDecl.Identifier.Text;

        // Get the full type name for DI registration
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        var fullTypeName = classSymbol?.ToDisplayString() ?? $"{namespaceName}.{className}";

        return new ProfileConfigurationModel
        {
            Namespace = namespaceName ?? "ZMapper.Generated",
            ClassName = className,
            FullTypeName = fullTypeName,
            Mappings = mappings
        };
    }

    /// <summary>
    /// Check if a class implements IMapperProfile using semantic analysis.
    ///
    /// For beginners: Syntax alone can be misleading (e.g., a class might have
    /// "IMapperProfile" in its base list but it could be a different interface).
    /// Semantic analysis verifies the actual type.
    /// </summary>
    private static bool ImplementsIMapperProfile(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null) return false;

        return classSymbol.AllInterfaces.Any(i =>
            i.Name == "IMapperProfile" &&
            i.ContainingNamespace.ToDisplayString() == "ZMapper.Abstractions.Configuration");
    }

    // ============================================================================
    // SHARED: Extraction logic used by both providers
    // ============================================================================

    /// <summary>
    /// Extract mapping information from a single CreateMap call.
    ///
    /// Example: config.CreateMap&lt;UserDto, User&gt;()...
    ///
    /// We need to know:
    /// - Source type (UserDto)
    /// - Destination type (User)
    /// - Properties on both types
    /// - Any special configuration (ForMember calls)
    ///
    /// For beginners: This reads one line of the recipe and figures out
    /// "I'm converting UserDto into User" and remembers all the ingredients (properties).
    /// </summary>
    private static MappingModel? ExtractMappingFromCall(
        InvocationExpressionSyntax call,
        SemanticModel semanticModel) // SemanticModel knows about types, not just syntax
    {
        // Check if this is a member access (something.CreateMap)
        if (call.Expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        // Get the generic arguments: CreateMap<TSource, TDestination>
        //                                        ^^^^^^  ^^^^^^^^^^^^
        if (memberAccess.Name is not GenericNameSyntax genericName)
            return null; // Not generic? Can't be CreateMap<,>

        // Get the two type arguments
        var typeArgs = genericName.TypeArgumentList.Arguments;
        if (typeArgs.Count != 2)
            return null; // Must have exactly 2 types!

        // Use semantic model to get the actual TYPE information (not just the name)
        // This resolves "User" to the actual User class, considering using statements, etc.
        var sourceType = semanticModel.GetTypeInfo(typeArgs[0]).Type;
        var destType = semanticModel.GetTypeInfo(typeArgs[1]).Type;

        // Make sure we got valid types
        if (sourceType == null || destType == null)
            return null;

        // Create a mapping model with all the information we need
        var mapping = new MappingModel
        {
            // Full type name with namespace (e.g., "MyApp.Models.UserDto")
            SourceType = sourceType.ToDisplayString(),
            // Just the class name (e.g., "UserDto")
            SourceTypeName = sourceType.Name,

            DestinationType = destType.ToDisplayString(),
            DestinationTypeName = destType.Name,

            // Get all public properties from both types
            SourceProperties = GetProperties(sourceType),
            DestinationProperties = GetProperties(destType),

            // Extract any ForMember configurations
            MemberConfigurations = ExtractMemberConfigurations(call, semanticModel)
        };

        // Extract BeforeMap/AfterMap hooks
        ExtractHooks(call, mapping);

        return mapping;
    }

    /// <summary>
    /// Report compile-time diagnostics for destination properties that have no matching source property.
    /// Skipped if IgnoreNonExisting() was called on the mapping.
    /// </summary>
    private static void ReportUnmappedPropertyDiagnostics(SourceProductionContext context, MappingModel mapping)
    {
        // If user explicitly opted out, skip all diagnostics for this mapping
        if (mapping.IgnoreNonExisting)
            return;

        foreach (var destProp in mapping.DestinationProperties)
        {
            // Check if explicitly configured (ForMember or Ignore)
            var memberConfig = mapping.MemberConfigurations.FirstOrDefault(
                c => c.DestinationMember == destProp.Name);

            if (memberConfig != null)
                continue; // Explicitly configured - no diagnostic needed

            // Check if there's a matching source property by name
            var sourcePropName = destProp.Name;
            var sourceProp = mapping.SourceProperties.FirstOrDefault(p => p.Name == sourcePropName);

            if (sourceProp == null)
            {
                // No match found - emit diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    UnmappedPropertyDiagnostic,
                    Location.None,
                    destProp.Name,
                    mapping.DestinationTypeName,
                    mapping.SourceTypeName));
            }
        }
    }

    /// <summary>
    /// Extract BeforeMap and AfterMap hooks from a CreateMap call chain.
    /// Generates static field names to store the Action delegates.
    /// </summary>
    private static void ExtractHooks(InvocationExpressionSyntax createMapCall, MappingModel mapping)
    {
        // Find the entire statement containing the CreateMap
        var statement = createMapCall.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (statement == null) return;

        // Look for BeforeMap() calls
        var beforeMapCall = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                 mae.Name.Identifier.Text == "BeforeMap");

        if (beforeMapCall != null)
        {
            // Generate a unique field name for this hook
            mapping.BeforeMapFieldName = $"_beforeMap_{mapping.SourceTypeName}To{mapping.DestinationTypeName}";
        }

        // Look for AfterMap() calls
        var afterMapCall = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                 mae.Name.Identifier.Text == "AfterMap");

        if (afterMapCall != null)
        {
            // Generate a unique field name for this hook
            mapping.AfterMapFieldName = $"_afterMap_{mapping.SourceTypeName}To{mapping.DestinationTypeName}";
        }

        // Look for IgnoreNonExisting() call - suppresses unmapped property diagnostics
        var ignoreNonExistingCall = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                 mae.Name.Identifier.Text == "IgnoreNonExisting");

        if (ignoreNonExistingCall != null)
        {
            mapping.IgnoreNonExisting = true;
        }
    }

    /// <summary>
    /// Check if the CreateMap call chain has ReverseMap() and create reverse mapping if present.
    ///
    /// Example:
    /// config.CreateMap<OrderDto, Order>()
    ///     .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.OrderId))
    ///     .ReverseMap();  // This creates the reverse mapping Order -> OrderDto
    ///
    /// For beginners: ReverseMap() means "also create a mapping in the opposite direction,
    /// swapping source and destination properties".
    /// </summary>
    private static MappingModel? ExtractReverseMappingIfPresent(
        InvocationExpressionSyntax createMapCall,
        MappingModel originalMapping,
        SemanticModel semanticModel)
    {
        // Find the entire statement containing the CreateMap
        var statement = createMapCall.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (statement == null) return null;

        // Check if there's a ReverseMap() call in the statement
        var hasReverseMap = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                       mae.Name.Identifier.Text == "ReverseMap");

        if (!hasReverseMap) return null;

        // Create reverse mapping by swapping source and destination
        var reverseMapping = new MappingModel
        {
            // Swap source and destination
            SourceType = originalMapping.DestinationType,
            SourceTypeName = originalMapping.DestinationTypeName,
            DestinationType = originalMapping.SourceType,
            DestinationTypeName = originalMapping.SourceTypeName,

            // Swap property lists
            SourceProperties = originalMapping.DestinationProperties,
            DestinationProperties = originalMapping.SourceProperties,

            // Invert member configurations
            MemberConfigurations = new List<MemberConfigModel>()
        };

        // Invert each member configuration
        foreach (var memberConfig in originalMapping.MemberConfigurations)
        {
            // Skip ignored members
            if (memberConfig.IsIgnored) continue;

            // For reverse mapping:
            // Original: dest.OrderId <- src.Id (DestinationMember="OrderId", SourceMember="Id")
            // Reverse:  dest.Id <- src.OrderId (DestinationMember="Id", SourceMember="OrderId")
            var reverseMemberConfig = new MemberConfigModel
            {
                DestinationMember = memberConfig.SourceMember ?? memberConfig.DestinationMember,
                SourceMember = memberConfig.DestinationMember,
                IsIgnored = false
            };

            reverseMapping.MemberConfigurations.Add(reverseMemberConfig);
        }

        return reverseMapping;
    }

    /// <summary>
    /// Get all public, settable properties from a type.
    ///
    /// We only care about properties we can SET (because we're mapping TO them).
    /// This includes:
    /// - Regular properties with setters
    /// - Init-only properties (set in object initializer)
    /// - Required properties (must be set)
    ///
    /// For beginners: This makes a list of all the "fields" on a class that we can fill in,
    /// like listing all the blank spaces in a form.
    /// </summary>
    private static List<PropertyModel> GetProperties(ITypeSymbol type)
    {
        // Walk the entire inheritance chain to collect properties from base classes too.
        // type.GetMembers() only returns DECLARED members on that specific type,
        // so we must traverse BaseType to include inherited properties (e.g. Id from BaseEntity).
        // We use a dictionary keyed by property name so that if a derived class re-declares
        // a property (using 'new'), the derived version wins (it's added first).
        var propertyMap = new Dictionary<string, IPropertySymbol>();
        var current = type;

        while (current != null)
        {
            foreach (var prop in current.GetMembers().OfType<IPropertySymbol>())
            {
                // Only take the first occurrence (most derived) of each property name
                if (!propertyMap.ContainsKey(prop.Name))
                {
                    propertyMap[prop.Name] = prop;
                }
            }

            // Move up to the parent class (null when we reach System.Object)
            current = current.BaseType;
        }

        return propertyMap.Values
            .Where(p =>
                // Must be public (we can't set private properties)
                p.DeclaredAccessibility == Accessibility.Public &&

                // Must not be static (static properties belong to the class, not instances)
                !p.IsStatic &&

                // Must be settable OR required
                // Settable means: has a set method OR is required (required forces initialization)
                (p.SetMethod != null || p.IsRequired))

            // Convert each property to our PropertyModel format
            .Select(p => new PropertyModel
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(), // Full type name
                IsRequired = p.IsRequired, // C# 11+ required properties
                IsNullable = p.Type.NullableAnnotation == NullableAnnotation.Annotated, // string? vs string
                IsInitOnly = p.SetMethod?.IsInitOnly ?? false, // init-only setters
                IsComplexType = IsComplexType(p.Type), // NEW: Detect nested objects
                IsCollection = IsCollectionType(p.Type), // NEW: Detect collections
                CollectionElementType = ExtractCollectionElementType(p.Type) // NEW: Get T from List<T>
            })
            .ToList();
    }

    /// <summary>
    /// Check if a type is a complex object (not a primitive/value type/string).
    /// Complex types need recursive mapping.
    ///
    /// For beginners: This tells us if a property is another object that needs its own mapping.
    /// For example: Customer is complex, but int and string are not.
    /// </summary>
    private static bool IsComplexType(ITypeSymbol type)
    {
        // Remove nullable wrapper if present (int? -> int)
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
        {
            type = namedType.TypeArguments[0];
        }

        // Check if it's a primitive or common value type using Roslyn's built-in detection
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Char:
            case SpecialType.System_String:
            case SpecialType.System_Object:
                return false; // These are simple types
        }

        // Check for common value types that don't have SpecialType
        // These are "simple" types that should be copied directly, not mapped via extension methods
        var fullName = type.ToDisplayString();
        if (fullName.StartsWith("System.DateTime") ||
            fullName.StartsWith("System.DateTimeOffset") ||
            fullName.StartsWith("System.DateOnly") ||   // .NET 6+ date-only type
            fullName.StartsWith("System.TimeOnly") ||   // .NET 6+ time-only type
            fullName.StartsWith("System.TimeSpan") ||
            fullName.StartsWith("System.Guid"))
        {
            return false; // These are also simple value types
        }

        // It's a class or struct that's not a basic type - it's complex!
        return type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct;
    }

    /// <summary>
    /// Check if type is a collection (List, IEnumerable, Array, etc.)
    ///
    /// For beginners: This checks if a property is a list/array of items.
    /// For example: List&lt;Customer&gt; or Customer[] or IEnumerable&lt;Customer&gt;
    /// </summary>
    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Arrays are always collections
        if (type is IArrayTypeSymbol)
            return true;

        // Not a generic type? Can't be a collection
        if (type is not INamedTypeSymbol namedType)
            return false;

        // Check for generic collection types
        var fullName = namedType.ConstructedFrom.ToDisplayString();
        return fullName.StartsWith("System.Collections.Generic.List<") ||
               fullName.StartsWith("System.Collections.Generic.IEnumerable<") ||
               fullName.StartsWith("System.Collections.Generic.ICollection<") ||
               fullName.StartsWith("System.Collections.Generic.IList<") ||
               fullName.StartsWith("System.Collections.Generic.IReadOnlyList<") ||
               fullName.StartsWith("System.Collections.Generic.IReadOnlyCollection<");
    }

    /// <summary>
    /// Extract element type from collection types.
    /// Examples:
    /// - List&lt;Customer&gt; -> "Customer"
    /// - Customer[] -> "Customer"
    /// - IEnumerable&lt;Order&gt; -> "Order"
    ///
    /// Returns null if not a collection.
    /// </summary>
    private static string? ExtractCollectionElementType(ITypeSymbol type)
    {
        // Array: get the element type
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType.ToDisplayString();

        // Generic collection: get the type argument (the T in List<T>)
        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
            return namedType.TypeArguments[0].ToDisplayString();

        return null;
    }

    /// <summary>
    /// Extract ForMember configurations from a CreateMap call chain.
    ///
    /// Example:
    /// config.CreateMap&lt;UserDto, User&gt;()
    ///     .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
    ///     .ForMember(dest => dest.IgnoreMe, opt => opt.Ignore())
    ///
    /// This finds all the ForMember calls and extracts:
    /// - Which destination property is being configured
    /// - Where to map from (MapFrom)
    /// - Whether to ignore the property (Ignore)
    ///
    /// For beginners: This reads special instructions like "put the 'Id' value into 'UserId'"
    /// or "skip the 'Password' field".
    /// </summary>
    private static List<MemberConfigModel> ExtractMemberConfigurations(
        InvocationExpressionSyntax createMapCall,
        SemanticModel semanticModel)
    {
        var configs = new List<MemberConfigModel>();

        // Find the entire statement (line of code) containing the CreateMap
        // Example: var x = config.CreateMap<A,B>().ForMember(...).ForMember(...);
        //          ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        var statement = createMapCall.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (statement == null) return configs;

        // Find all ForMember calls in this statement
        // These are chained after CreateMap: .ForMember(...).ForMember(...)
        var forMemberCalls = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>() // Find method calls
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                         mae.Name.Identifier.Text == "ForMember") // Named "ForMember"
            .ToList();

        // Process each ForMember call
        foreach (var forMemberCall in forMemberCalls)
        {
            var config = ExtractForMemberConfig(forMemberCall, semanticModel);
            if (config != null)
            {
                configs.Add(config);
            }
        }

        return configs;
    }

    /// <summary>
    /// Extract configuration from a single ForMember call.
    ///
    /// ForMember has this structure:
    /// .ForMember(dest => dest.PropertyName,    // First argument: which property
    ///            opt => opt.MapFrom(src => src.OtherProp))  // Second argument: what to do
    ///
    /// For beginners: This reads one instruction like
    /// "For the 'UserId' property, copy the value from 'Id'".
    /// </summary>
    private static MemberConfigModel? ExtractForMemberConfig(
        InvocationExpressionSyntax forMemberCall,
        SemanticModel semanticModel)
    {
        // ForMember must have 2 arguments
        if (forMemberCall.ArgumentList.Arguments.Count < 2)
            return null;

        // First argument: dest => dest.PropertyName
        var firstArg = forMemberCall.ArgumentList.Arguments[0].Expression;

        // Must be a lambda expression (the => part)
        if (firstArg is not SimpleLambdaExpressionSyntax &&
            firstArg is not ParenthesizedLambdaExpressionSyntax)
            return null;

        // Extract the property name from the lambda
        var lambda = (LambdaExpressionSyntax)firstArg;
        var memberName = ExtractMemberNameFromLambda(lambda);

        if (string.IsNullOrEmpty(memberName))
            return null; // Couldn't figure out which property

        // Create the config model
        var config = new MemberConfigModel { DestinationMember = memberName! };

        // Second argument: opt => opt.MapFrom(...) or opt => opt.Ignore()
        var secondArg = forMemberCall.ArgumentList.Arguments[1].Expression;
        if (secondArg is SimpleLambdaExpressionSyntax lambda2)
        {
            // Analyze what the lambda does (MapFrom, Ignore, etc.)
            AnalyzeMemberConfiguration(lambda2.Body, config, semanticModel);
        }

        return config;
    }

    /// <summary>
    /// Extract the property name from a lambda expression.
    ///
    /// Example: dest => dest.UserId
    ///                       ^^^^^^ (we want "UserId")
    ///
    /// For beginners: This reads "dest.UserId" and extracts just "UserId".
    /// </summary>
    private static string? ExtractMemberNameFromLambda(LambdaExpressionSyntax lambda)
    {
        // The lambda body should be accessing a property: dest.PropertyName
        if (lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            // Return just the property name
            return memberAccess.Name.Identifier.Text;
        }
        return null;
    }

    /// <summary>
    /// Analyze what a ForMember configuration does.
    ///
    /// It could be:
    /// - opt.MapFrom(src => src.OtherProperty) - map from different property
    /// - opt.Ignore() - don't map this property
    /// - opt.ConvertUsing(...) - custom conversion (not yet implemented)
    ///
    /// For beginners: This figures out the special instruction -
    /// either "copy from somewhere else" or "skip this one".
    /// </summary>
    private static void AnalyzeMemberConfiguration(
        SyntaxNode body,
        MemberConfigModel config,
        SemanticModel semanticModel)
    {
        // Find all method calls in the lambda body
        // Example: opt.MapFrom(src => src.Id) has one invocation "MapFrom"
        var invocations = body.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>();

        foreach (var inv in invocations)
        {
            if (inv.Expression is MemberAccessExpressionSyntax mae)
            {
                // Check what method is being called
                if (mae.Name.Identifier.Text == "Ignore")
                {
                    // opt.Ignore() - mark this property as ignored
                    config.IsIgnored = true;
                }
                else if (mae.Name.Identifier.Text == "MapFrom")
                {
                    // opt.MapFrom(src => src.OtherProperty)
                    // opt.MapFrom(src => src.Client.CompanyName)     -- navigation property
                    // opt.MapFrom(src => src.IssueDate ?? DateTime.UtcNow)  -- complex expression
                    if (inv.ArgumentList.Arguments.Count > 0)
                    {
                        var arg = inv.ArgumentList.Arguments[0].Expression;
                        if (arg is SimpleLambdaExpressionSyntax sourceLambda)
                        {
                            // Try simple property name first (src => src.PropertyName)
                            var simpleName = ExtractMemberNameFromLambda(sourceLambda);
                            if (simpleName != null)
                            {
                                config.SourceMember = simpleName;
                            }
                            else
                            {
                                // Complex expression: store the full lambda body with
                                // the parameter name replaced by "source" so it works
                                // in the generated code context.
                                var parameterName = sourceLambda.Parameter.Identifier.Text;
                                var expressionBody = sourceLambda.Body.ToString();
                                config.SourceExpression = expressionBody.Replace(parameterName + ".", "source.");
                            }
                        }
                    }
                }
                else if (mae.Name.Identifier.Text == "When")
                {
                    // opt.When(src => src.Value != null)
                    // Extract the condition expression BODY (not the full lambda)
                    if (inv.ArgumentList.Arguments.Count > 0)
                    {
                        var arg = inv.ArgumentList.Arguments[0].Expression;
                        if (arg is SimpleLambdaExpressionSyntax lambda)
                        {
                            // Extract body and replace parameter name with "source"
                            // Example: "src.Value != null" becomes "source.Value != null"
                            var parameterName = lambda.Parameter.Identifier.Text;
                            var conditionBody = lambda.Body.ToString();
                            config.Condition = conditionBody.Replace(parameterName, "source");
                        }
                    }
                }
                // Future: Add support for ConvertUsing, etc.
            }
        }
    }

    // ============================================================================
    // CODE GENERATION
    // ============================================================================

    /// <summary>
    /// Generate the actual C# code files.
    ///
    /// This is called once for all the mapper configurations we found.
    /// We generate:
    /// 1. One .cs file per existing mapper class (CreateMap pattern - backward compatible)
    /// 2. Global extension methods for zero-overhead mapping
    /// 3. A unified mapper from all IMapperProfile implementations
    /// 4. AddZMapper() DI extension (only if M.E.DI is referenced)
    ///
    /// For beginners: This is where we finally WRITE the code!
    /// We take all the recipes (configurations) and write actual C# methods.
    /// </summary>
    private static void Execute(
        System.Collections.Immutable.ImmutableArray<MapperConfigurationModel?> models,
        System.Collections.Immutable.ImmutableArray<ProfileConfigurationModel?> profileModels,
        Compilation compilation,
        SourceProductionContext context)
    {
        // ---- Part 1: Generate code for CreateMap-based configurations (existing pattern) ----

        // Group configurations by class (namespace + class name)
        // This prevents generating duplicate code if the same class is analyzed multiple times
        var grouped = models
            .Where(m => m != null)
            .GroupBy(m => $"{m!.Namespace}.{m!.ClassName}")
            .ToList();

        // Generate code for each class
        foreach (var group in grouped)
        {
            var first = group.First()!;

            // Get unique mappings (avoid duplicates if same mapping configured twice)
            var uniqueMappings = group
                .SelectMany(m => m!.Mappings)
                .GroupBy(m => $"{m.SourceType}_{m.DestinationType}") // Key by type pair
                .Select(g => g.First()) // Take first of each duplicate
                .ToList();

            // Create merged model with all unique mappings
            var merged = new MapperConfigurationModel
            {
                Namespace = first.Namespace,
                ClassName = first.ClassName,
                MethodName = first.MethodName,
                MethodTakesConfig = first.MethodTakesConfig,
                DebugParamInfo = first.DebugParamInfo,
                Mappings = uniqueMappings
            };

            // Generate the C# code as a string
            var source = GenerateMapperCode(merged);

            // Add the generated file to the compilation
            // File name: ClassName_Generated.g.cs (the .g indicates "generated")
            context.AddSource(
                $"{merged.ClassName}_Generated.g.cs",
                SourceText.From(source, Encoding.UTF8));
        }

        // ---- Part 1b: Report diagnostics for unmapped destination properties ----
        // For each mapping, check if any destination properties have no source match
        // and IgnoreNonExisting() was not called.
        foreach (var group in grouped)
        {
            foreach (var model in group.Where(m => m != null))
            {
                foreach (var mapping in model!.Mappings)
                {
                    ReportUnmappedPropertyDiagnostics(context, mapping);
                }
            }
        }
        foreach (var profile in profileModels.Where(p => p != null))
        {
            foreach (var mapping in profile!.Mappings)
            {
                ReportUnmappedPropertyDiagnostics(context, mapping);
            }
        }

        // ---- Part 2: Collect ALL unique mappings for extension methods ----

        // Combine mappings from both CreateMap-based and Profile-based configurations
        var allMappingsFromCreateMap = models
            .Where(m => m != null)
            .SelectMany(m => m!.Mappings)
            .ToList();

        var allMappingsFromProfiles = profileModels
            .Where(p => p != null)
            .SelectMany(p => p!.Mappings)
            .ToList();

        var allMappings = allMappingsFromCreateMap
            .Concat(allMappingsFromProfiles)
            .GroupBy(m => $"{m.SourceType}_{m.DestinationType}")
            .Select(g => g.First())
            .ToList();

        // ---- Part 3: Generate global extension methods ----

        if (allMappings.Any())
        {
            // Use first available namespace
            var extensionNamespace = models.FirstOrDefault(m => m != null)?.Namespace
                ?? profileModels.FirstOrDefault(p => p != null)?.Namespace
                ?? "ZMapper.Generated";

            var extensionModel = new MapperConfigurationModel
            {
                Namespace = extensionNamespace,
                ClassName = "Mapper",  // Global name for extension methods
                MethodName = "",
                Mappings = allMappings
            };

            var extensionSource = GenerateExtensionMethodsCode(extensionModel);
            context.AddSource(
                "ZMapper_GlobalExtensions.g.cs",
                SourceText.From(extensionSource, Encoding.UTF8));
        }

        // ---- Part 4: Generate Unified Mapper and DI extension for Profile-based configurations ----

        var validProfiles = profileModels.Where(p => p != null).ToList();
        if (validProfiles.Any())
        {
            // Collect all profile mappings (deduplicated)
            var profileMappings = validProfiles
                .SelectMany(p => p!.Mappings)
                .GroupBy(m => $"{m.SourceType}_{m.DestinationType}")
                .Select(g => g.First())
                .ToList();

            // Determine namespace for generated unified mapper
            var unifiedNamespace = validProfiles.First()!.Namespace;

            // Generate the unified mapper class
            var unifiedSource = GenerateUnifiedMapperCode(unifiedNamespace, profileMappings);
            context.AddSource(
                "ZMapper_Mapper.g.cs",
                SourceText.From(unifiedSource, Encoding.UTF8));

            // Check if M.E.DI.Abstractions is referenced in the consuming project
            bool hasDependencyInjection = compilation.ReferencedAssemblyNames
                .Any(a => a.Name == "Microsoft.Extensions.DependencyInjection.Abstractions");

            if (hasDependencyInjection)
            {
                // Generate AddZMapper() extension method
                var diSource = GenerateServiceCollectionExtension(unifiedNamespace, validProfiles);
                context.AddSource(
                    "ZMapper_ServiceCollectionExtensions.g.cs",
                    SourceText.From(diSource, Encoding.UTF8));
            }
        }
    }

    // ============================================================================
    // CODE GENERATION: Per-class mapper (existing CreateMap pattern)
    // ============================================================================

    /// <summary>
    /// Generate the complete C# code for a mapper class.
    ///
    /// This generates:
    /// - A nested GeneratedMapper class implementing IMapper
    /// - Specific mapping methods for each configured mapping
    /// - Generic dispatch methods that route to the specific methods
    /// - A factory method CreateGeneratedMapper()
    ///
    /// For beginners: This builds the entire .cs file, piece by piece,
    /// like assembling a document from templates.
    /// </summary>
    private static string GenerateMapperCode(MapperConfigurationModel model)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8601 // Possible null reference assignment in generated mapping code");
        sb.AppendLine();

        // Using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using ZMapper;");
        sb.AppendLine("using ZMapper.Abstractions;");
        sb.AppendLine();

        // Namespace and class declaration
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ClassName}"); // partial so it extends user's class
        sb.AppendLine("{");

        // Nested GeneratedMapper class
        sb.AppendLine("    private sealed class GeneratedMapper : IMapper");
        sb.AppendLine("    {");

        // Generate static fields for hooks
        foreach (var mapping in model.Mappings)
        {
            if (!string.IsNullOrEmpty(mapping.BeforeMapFieldName))
            {
                sb.AppendLine($"        internal static System.Action<{mapping.SourceType}, {mapping.DestinationType}>? {mapping.BeforeMapFieldName};");
            }
            if (!string.IsNullOrEmpty(mapping.AfterMapFieldName))
            {
                sb.AppendLine($"        internal static System.Action<{mapping.SourceType}, {mapping.DestinationType}>? {mapping.AfterMapFieldName};");
            }
        }
        if (model.Mappings.Any(m => !string.IsNullOrEmpty(m.BeforeMapFieldName) || !string.IsNullOrEmpty(m.AfterMapFieldName)))
        {
            sb.AppendLine();
        }

        // Generate 5 methods for each mapping:
        foreach (var mapping in model.Mappings)
        {
            GenerateMapMethod(sb, mapping);              // Map(source) -> new destination
            GenerateMapToExistingMethod(sb, mapping);    // Map(source, destination) -> existing destination
            GenerateMapArrayMethod(sb, mapping);         // MapArray(span) -> array
            GenerateMapListMethod(sb, mapping);          // MapList(IReadOnlyList) -> list
            GenerateMapListEnumerableMethod(sb, mapping); // MapList(IEnumerable) -> list
        }

        // Generate generic methods that dispatch to specific methods
        GenerateGenericDispatchMethods(sb, model);

        // Close GeneratedMapper class
        sb.AppendLine("    }");
        sb.AppendLine();

        // Check if any mapping has hooks (needed for both factory method overloads)
        bool hasAnyHooks = model.Mappings.Any(m => !string.IsNullOrEmpty(m.BeforeMapFieldName) || !string.IsNullOrEmpty(m.AfterMapFieldName));

        // Factory method (no-arg): creates mapper, optionally calling ConfigureMapper to populate hooks.
        // When MethodTakesConfig is true, we call ConfigureMapper(config) to get hook delegates.
        // When MethodTakesConfig is false and hooks exist, user must call the overload with config.
        sb.AppendLine($"    private static IMapper CreateGeneratedMapper()");
        sb.AppendLine("    {");

        if (hasAnyHooks && model.MethodTakesConfig)
        {
            // Call the ConfigureMapper method to populate hooks, then delegate to config overload
            sb.AppendLine("        var config = new MapperConfiguration();");
            sb.AppendLine($"        {model.MethodName}(config);");
            sb.AppendLine("        return CreateGeneratedMapper(config);");
        }
        else
        {
            sb.AppendLine("        return new GeneratedMapper();");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Bug 2 fix: Generate overload that accepts MapperConfiguration directly.
        // This allows hooks to work even without the ConfigureMapper(MapperConfiguration) pattern.
        // Users can pass their config object: CreateGeneratedMapper(config)
        if (hasAnyHooks)
        {
            sb.AppendLine($"    private static IMapper CreateGeneratedMapper(MapperConfiguration config)");
            sb.AppendLine("    {");

            // Extract hook delegates from the config and wire them to static fields
            // Bug 1 fix: Use index suffix on local variable names to avoid CS0128
            // when multiple mappings have hooks in the same config class.
            int hookIndex = 0;
            foreach (var mapping in model.Mappings)
            {
                if (string.IsNullOrEmpty(mapping.BeforeMapFieldName) && string.IsNullOrEmpty(mapping.AfterMapFieldName))
                    continue;

                sb.AppendLine($"        var mapping_{mapping.SourceTypeName}_{mapping.DestinationTypeName} = config.Mappings");
                sb.AppendLine($"            .FirstOrDefault(m => m.SourceType == typeof({mapping.SourceType}) && m.DestinationType == typeof({mapping.DestinationType}));");

                if (!string.IsNullOrEmpty(mapping.BeforeMapFieldName))
                {
                    sb.AppendLine($"        if (mapping_{mapping.SourceTypeName}_{mapping.DestinationTypeName}?.BeforeMapAction is System.Action<{mapping.SourceType}, {mapping.DestinationType}> beforeMap_{hookIndex})");
                    sb.AppendLine($"            GeneratedMapper.{mapping.BeforeMapFieldName} = beforeMap_{hookIndex};");
                }

                if (!string.IsNullOrEmpty(mapping.AfterMapFieldName))
                {
                    sb.AppendLine($"        if (mapping_{mapping.SourceTypeName}_{mapping.DestinationTypeName}?.AfterMapAction is System.Action<{mapping.SourceType}, {mapping.DestinationType}> afterMap_{hookIndex})");
                    sb.AppendLine($"            GeneratedMapper.{mapping.AfterMapFieldName} = afterMap_{hookIndex};");
                }

                sb.AppendLine();
                hookIndex++;
            }

            sb.AppendLine("        return new GeneratedMapper();");
            sb.AppendLine("    }");
        }

        // Close user's class
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Extract simple type name from fully qualified name.
    /// Examples:
    /// - "MyApp.Models.Customer" -> "Customer"
    /// - "System.Collections.Generic.List&lt;Customer&gt;" -> "Customer"
    ///
    /// For beginners: This removes the namespace to get just the class name.
    /// We need this to generate extension method calls like .ToCustomer()
    /// </summary>
    private static string GetSimpleTypeName(string fullTypeName)
    {
        // Remove namespace: "MyApp.Models.Customer" -> "Customer"
        var parts = fullTypeName.Split('.');
        var typeName = parts[parts.Length - 1];

        // Remove nullable marker: "Customer?" -> "Customer"
        typeName = typeName.TrimEnd('?');

        // Remove generic arguments: "List<T>" -> "List"
        var genericIndex = typeName.IndexOf('<');
        if (genericIndex > 0)
            typeName = typeName.Substring(0, genericIndex);

        return typeName;
    }

    /// <summary>
    /// Generate property assignment code based on property types.
    /// Handles three cases:
    /// 1. Collection of complex objects: List&lt;CustomerDto&gt; -> List&lt;Customer&gt;
    /// 2. Nested complex object: CustomerDto -> Customer
    /// 3. Simple property: int -> int, string -> string
    ///
    /// For beginners: This is the "smart" part that decides HOW to map each property.
    /// </summary>
    private static void GeneratePropertyAssignment(
        StringBuilder sb,
        PropertyModel destProp,
        PropertyModel sourceProp,
        MemberConfigModel? memberConfig)
    {
        var indent = "            ";

        // Handle conditional mapping (When(...))
        if (!string.IsNullOrEmpty(memberConfig?.Condition))
        {
            sb.AppendLine($"{indent}if ({memberConfig!.Condition})");
            indent += "    "; // Add extra indent for code inside if
        }

        // CASE 0: Complex MapFrom expression (e.g., src => src.Client.CompanyName or src => src.Date ?? DateTime.UtcNow)
        // When the user provides a full expression in MapFrom, we emit it directly as the right-hand side.
        if (!string.IsNullOrEmpty(memberConfig?.SourceExpression))
        {
            sb.AppendLine($"{indent}destination.{destProp.Name} = {memberConfig!.SourceExpression};");
            return;
        }

        // CASE 1: Collection of complex objects
        // Example: List<CustomerDto> -> List<Customer>
        // Generated: destination.Customers = source.Customers?.Select(item => item.ToCustomer()).ToList();
        if (destProp.IsCollection && sourceProp.IsCollection &&
            destProp.CollectionElementType != null && sourceProp.CollectionElementType != null)
        {
            var sourceElementType = GetSimpleTypeName(sourceProp.CollectionElementType);
            var destElementType = GetSimpleTypeName(destProp.CollectionElementType);

            // Only need mapping if element types are different
            if (sourceElementType != destElementType)
            {
                sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name}");
                sb.AppendLine($"{indent}    ?.Select(item => item.To{destElementType}())");
                sb.AppendLine($"{indent}    .ToList();");
                return;
            }
        }

        // CASE 2: Nested complex object
        // Example: CustomerDto -> Customer
        // Generated: destination.Customer = source.Customer != null ? source.Customer.ToCustomer() : null;
        if (destProp.IsComplexType && sourceProp.IsComplexType)
        {
            // If source and destination types are EXACTLY the same, just copy directly
            // This handles cases like OrderStatusInfo -> OrderStatusInfo where no mapping is needed
            if (sourceProp.Type == destProp.Type)
            {
                sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name};");
                return;
            }

            var destTypeName = GetSimpleTypeName(destProp.Type);

            sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name} != null");
            sb.AppendLine($"{indent}    ? source.{sourceProp.Name}.To{destTypeName}()");
            sb.AppendLine($"{indent}    : null;");
            return;
        }

        // CASE 3: Simple property (int, string, DateTime, etc.)
        // When source is nullable value type (e.g. DateTime?) and destination is non-nullable (DateTime),
        // we must use ?? default to avoid CS0266 compile error. For reference types this is harmless.
        if (sourceProp.IsNullable && !destProp.IsNullable)
        {
            sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name} ?? default;");
        }
        else
        {
            sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name};");
        }
    }

    /// <summary>
    /// Same as GeneratePropertyAssignment but for extension methods (different indentation).
    ///
    /// NOTE: Uses fully qualified type names from mapping model for nested objects
    /// to avoid namespace conflicts when types have the same short name.
    /// </summary>
    private static void GeneratePropertyAssignmentForExtension(
        StringBuilder sb,
        PropertyModel destProp,
        PropertyModel sourceProp,
        MemberConfigModel? memberConfig,
        MappingModel mapping)
    {
        var indent = "        "; // Extension methods have less indentation

        // Handle conditional mapping (When(...))
        if (!string.IsNullOrEmpty(memberConfig?.Condition))
        {
            sb.AppendLine($"{indent}if ({memberConfig!.Condition})");
            indent += "    ";
        }

        // CASE 0: Complex MapFrom expression (e.g., src => src.Client.CompanyName)
        if (!string.IsNullOrEmpty(memberConfig?.SourceExpression))
        {
            sb.AppendLine($"{indent}destination.{destProp.Name} = {memberConfig!.SourceExpression};");
            return;
        }

        // CASE 1: Collection of complex objects
        if (destProp.IsCollection && sourceProp.IsCollection &&
            destProp.CollectionElementType != null && sourceProp.CollectionElementType != null)
        {
            var sourceElementType = GetSimpleTypeName(sourceProp.CollectionElementType);
            var destElementType = GetSimpleTypeName(destProp.CollectionElementType);

            if (sourceElementType != destElementType)
            {
                sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name}");
                sb.AppendLine($"{indent}    ?.Select(item => item.To{destElementType}())");
                sb.AppendLine($"{indent}    .ToList();");
                return;
            }
        }

        // CASE 2: Nested complex object
        if (destProp.IsComplexType && sourceProp.IsComplexType)
        {
            if (sourceProp.Type == destProp.Type)
            {
                sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name};");
                return;
            }

            var destTypeName = GetSimpleTypeName(destProp.Type);

            sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name} != null");
            sb.AppendLine($"{indent}    ? source.{sourceProp.Name}.To{destTypeName}()");
            sb.AppendLine($"{indent}    : null;");
            return;
        }

        // CASE 3: Simple property
        // When source is nullable value type (e.g. DateTime?) and destination is non-nullable (DateTime),
        // we must use ?? default to avoid CS0266 compile error.
        if (sourceProp.IsNullable && !destProp.IsNullable)
        {
            sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name} ?? default;");
        }
        else
        {
            sb.AppendLine($"{indent}destination.{destProp.Name} = source.{sourceProp.Name};");
        }
    }

    /// <summary>
    /// Generate a method that maps from source to NEW destination object.
    ///
    /// For beginners: This writes code that creates a new object and copies properties.
    /// </summary>
    private static void GenerateMapMethod(StringBuilder sb, MappingModel mapping)
    {
        sb.AppendLine();
        // Method signature: public User Map_UserDto_To_User(UserDto source)
        sb.AppendLine($"        public {mapping.DestinationType} Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}({mapping.SourceType} source)");
        sb.AppendLine("        {");

        // Call BeforeMap hook if present
        if (!string.IsNullOrEmpty(mapping.BeforeMapFieldName))
        {
            sb.AppendLine($"            var destination = new {mapping.DestinationType}();");
            sb.AppendLine($"            {mapping.BeforeMapFieldName}?.Invoke(source, destination);");
            sb.AppendLine();
        }

        // Check if destination has init-only or required properties
        var hasInitOnlyProps = mapping.DestinationProperties.Any(p => p.IsInitOnly || p.IsRequired);

        // If BeforeMap exists, destination is already created, so we can't use object initializer
        var hasBeforeMap = !string.IsNullOrEmpty(mapping.BeforeMapFieldName);

        if (hasInitOnlyProps && !hasBeforeMap)
        {
            // Generate object initializer pattern
            sb.AppendLine($"            var destination = new {mapping.DestinationType}");
            sb.AppendLine("            {");

            var propsToInitialize = new List<string>();

            foreach (var destProp in mapping.DestinationProperties)
            {
                var memberConfig = mapping.MemberConfigurations.FirstOrDefault(
                    c => c.DestinationMember == destProp.Name);

                if (memberConfig?.IsIgnored == true)
                    continue;

                var sourcePropName = memberConfig?.SourceMember ?? destProp.Name;
                var sourceProp = mapping.SourceProperties.FirstOrDefault(p => p.Name == sourcePropName);

                // Complex MapFrom expression takes precedence over simple property mapping
                if (!string.IsNullOrEmpty(memberConfig?.SourceExpression))
                {
                    propsToInitialize.Add($"                {destProp.Name} = {memberConfig!.SourceExpression}");
                }
                else if (sourceProp != null)
                {
                    propsToInitialize.Add($"                {destProp.Name} = source.{sourceProp.Name}");
                }
            }

            for (int i = 0; i < propsToInitialize.Count; i++)
            {
                if (i < propsToInitialize.Count - 1)
                    sb.AppendLine(propsToInitialize[i] + ",");
                else
                    sb.AppendLine(propsToInitialize[i]);
            }

            sb.AppendLine("            };");
        }
        else
        {
            // Generate separate statement pattern (faster)
            if (!hasBeforeMap)
            {
                sb.AppendLine($"            var destination = new {mapping.DestinationType}();");
            }

            foreach (var destProp in mapping.DestinationProperties)
            {
                var memberConfig = mapping.MemberConfigurations.FirstOrDefault(
                    c => c.DestinationMember == destProp.Name);

                if (memberConfig?.IsIgnored == true)
                    continue;

                var sourcePropName = memberConfig?.SourceMember ?? destProp.Name;
                var sourceProp = mapping.SourceProperties.FirstOrDefault(p => p.Name == sourcePropName);

                // Generate assignment if we have a matching source property OR a complex expression
                if (sourceProp != null || !string.IsNullOrEmpty(memberConfig?.SourceExpression))
                {
                    GeneratePropertyAssignment(sb, destProp, sourceProp!, memberConfig);
                }
            }
        }

        // Call AfterMap hook if present
        if (!string.IsNullOrEmpty(mapping.AfterMapFieldName))
        {
            sb.AppendLine($"            {mapping.AfterMapFieldName}?.Invoke(source, destination);");
        }

        sb.AppendLine("            return destination;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generate a method that maps to an EXISTING destination object.
    /// </summary>
    private static void GenerateMapToExistingMethod(StringBuilder sb, MappingModel mapping)
    {
        sb.AppendLine();
        sb.AppendLine($"        public {mapping.DestinationType} Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}({mapping.SourceType} source, {mapping.DestinationType} destination)");
        sb.AppendLine("        {");

        // Call BeforeMap hook if present
        if (!string.IsNullOrEmpty(mapping.BeforeMapFieldName))
        {
            sb.AppendLine($"            {mapping.BeforeMapFieldName}?.Invoke(source, destination);");
        }

        foreach (var destProp in mapping.DestinationProperties)
        {
            var memberConfig = mapping.MemberConfigurations.FirstOrDefault(
                c => c.DestinationMember == destProp.Name);

            if (memberConfig?.IsIgnored == true)
                continue;

            var sourcePropName = memberConfig?.SourceMember ?? destProp.Name;
            var sourceProp = mapping.SourceProperties.FirstOrDefault(p => p.Name == sourcePropName);

            if (sourceProp != null || !string.IsNullOrEmpty(memberConfig?.SourceExpression))
            {
                GeneratePropertyAssignment(sb, destProp, sourceProp!, memberConfig);
            }
        }

        // Call AfterMap hook if present
        if (!string.IsNullOrEmpty(mapping.AfterMapFieldName))
        {
            sb.AppendLine($"            {mapping.AfterMapFieldName}?.Invoke(source, destination);");
        }

        sb.AppendLine("            return destination;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generate a method that maps an array/span of objects using ReadOnlySpan for zero-copy performance.
    /// </summary>
    private static void GenerateMapArrayMethod(StringBuilder sb, MappingModel mapping)
    {
        sb.AppendLine();
        sb.AppendLine($"        public {mapping.DestinationType}[] MapArray_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(System.ReadOnlySpan<{mapping.SourceType}> source)");
        sb.AppendLine("        {");

        sb.AppendLine($"            var destination = new {mapping.DestinationType}[source.Length];");

        bool hasHooks = !string.IsNullOrEmpty(mapping.BeforeMapFieldName) || !string.IsNullOrEmpty(mapping.AfterMapFieldName);
        sb.AppendLine("            for (int i = 0; i < source.Length; i++)");
        sb.AppendLine("            {");
        if (hasHooks)
            sb.AppendLine($"                destination[i] = Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(source[i]);");
        else
            sb.AppendLine($"                destination[i] = source[i].To{mapping.DestinationTypeName}();");
        sb.AppendLine("            }");
        sb.AppendLine();

        sb.AppendLine("            return destination;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generate a method that maps an IReadOnlyList to a List.
    /// </summary>
    private static void GenerateMapListMethod(StringBuilder sb, MappingModel mapping)
    {
        sb.AppendLine();
        sb.AppendLine($"        public List<{mapping.DestinationType}> MapList_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(System.Collections.Generic.IReadOnlyList<{mapping.SourceType}> source)");
        sb.AppendLine("        {");

        sb.AppendLine($"            var destination = new List<{mapping.DestinationType}>(source.Count);");

        bool hasHooks = !string.IsNullOrEmpty(mapping.BeforeMapFieldName) || !string.IsNullOrEmpty(mapping.AfterMapFieldName);
        sb.AppendLine("            for (int i = 0; i < source.Count; i++)");
        sb.AppendLine("            {");
        if (hasHooks)
            sb.AppendLine($"                destination.Add(Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(source[i]));");
        else
            sb.AppendLine($"                destination.Add(source[i].To{mapping.DestinationTypeName}());");
        sb.AppendLine("            }");

        sb.AppendLine("            return destination;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generate a method that maps an IEnumerable to a List.
    /// This overload accepts any IEnumerable (ICollection, HashSet, EF results, etc.)
    /// and iterates using foreach for maximum compatibility.
    ///
    /// For beginners: This is like MapList but works with ANY collection type,
    /// not just IReadOnlyList. It's slightly less efficient because we can't
    /// pre-allocate the exact size, but much more flexible.
    /// </summary>
    private static void GenerateMapListEnumerableMethod(StringBuilder sb, MappingModel mapping)
    {
        sb.AppendLine();
        sb.AppendLine($"        public List<{mapping.DestinationType}> MapList_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}_Enumerable(System.Collections.Generic.IEnumerable<{mapping.SourceType}> source)");
        sb.AppendLine("        {");

        sb.AppendLine($"            var destination = new List<{mapping.DestinationType}>();");

        bool hasHooks = !string.IsNullOrEmpty(mapping.BeforeMapFieldName) || !string.IsNullOrEmpty(mapping.AfterMapFieldName);
        sb.AppendLine("            foreach (var item in source)");
        sb.AppendLine("            {");
        if (hasHooks)
            sb.AppendLine($"                destination.Add(Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(item));");
        else
            sb.AppendLine($"                destination.Add(item.To{mapping.DestinationTypeName}());");
        sb.AppendLine("            }");

        sb.AppendLine("            return destination;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generate generic dispatch methods.
    ///
    /// These methods have generic type parameters: Map&lt;TSource, TDestination&gt;
    /// They check the types at runtime and call the appropriate specific method.
    ///
    /// For beginners: This is the "router" - when you call Map&lt;A,B&gt;, it figures out
    /// which specific method to call based on A and B.
    /// </summary>
    private static void GenerateGenericDispatchMethods(StringBuilder sb, MapperConfigurationModel model)
    {
        // Generate Map<TSource, TDestination>(source)
        sb.AppendLine();
        sb.AppendLine("        public TDestination Map<TSource, TDestination>(TSource source)");
        sb.AppendLine("        {");

        foreach (var mapping in model.Mappings)
        {
            sb.AppendLine($"            if (typeof(TSource) == typeof({mapping.SourceType}) && typeof(TDestination) == typeof({mapping.DestinationType}))");

            bool hasHooks = !string.IsNullOrEmpty(mapping.BeforeMapFieldName) || !string.IsNullOrEmpty(mapping.AfterMapFieldName);
            if (hasHooks)
            {
                sb.AppendLine($"                return (TDestination)(object)Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(({mapping.SourceType})(object)source!);");
            }
            else
            {
                sb.AppendLine($"                return (TDestination)(object)(({mapping.SourceType})(object)source!).To{mapping.DestinationTypeName}();");
            }
        }

        sb.AppendLine("            throw new NotSupportedException($\"Mapping from {typeof(TSource).Name} to {typeof(TDestination).Name} is not configured.\");");
        sb.AppendLine("        }");

        // Generate Map<TSource, TDestination>(source, destination)
        sb.AppendLine();
        sb.AppendLine("        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)");
        sb.AppendLine("        {");

        foreach (var mapping in model.Mappings)
        {
            sb.AppendLine($"            if (typeof(TSource) == typeof({mapping.SourceType}) && typeof(TDestination) == typeof({mapping.DestinationType}))");
            sb.AppendLine($"                return (TDestination)(object)Map_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(({mapping.SourceType})(object)source!, ({mapping.DestinationType})(object)destination!);");
        }

        sb.AppendLine("            throw new NotSupportedException($\"Mapping from {typeof(TSource).Name} to {typeof(TDestination).Name} is not configured.\");");
        sb.AppendLine("        }");

        // Generate MapArray<TSource, TDestination>(span)
        sb.AppendLine();
        sb.AppendLine("        public TDestination[] MapArray<TSource, TDestination>(System.ReadOnlySpan<TSource> source)");
        sb.AppendLine("        {");

        bool first = true;
        foreach (var mapping in model.Mappings)
        {
            var ifKeyword = first ? "if" : "else if";
            first = false;

            sb.AppendLine($"            {ifKeyword} (typeof(TSource) == typeof({mapping.SourceType}) && typeof(TDestination) == typeof({mapping.DestinationType}))");
            sb.AppendLine("            {");
            sb.AppendLine($"                var typedSource = System.Runtime.CompilerServices.Unsafe.As<System.ReadOnlySpan<TSource>, System.ReadOnlySpan<{mapping.SourceType}>>(ref source);");
            sb.AppendLine($"                return (TDestination[])(object)MapArray_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}(typedSource);");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            throw new NotSupportedException($\"Array mapping from {typeof(TSource).Name} to {typeof(TDestination).Name} is not configured.\");");
        sb.AppendLine("        }");

        // Generate MapList<TSource, TDestination>(IReadOnlyList)
        sb.AppendLine();
        sb.AppendLine("        public List<TDestination> MapList<TSource, TDestination>(System.Collections.Generic.IReadOnlyList<TSource> source)");
        sb.AppendLine("        {");

        foreach (var mapping in model.Mappings)
        {
            sb.AppendLine($"            if (typeof(TSource) == typeof({mapping.SourceType}) && typeof(TDestination) == typeof({mapping.DestinationType}))");
            sb.AppendLine($"                return (List<TDestination>)(object)MapList_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}((System.Collections.Generic.IReadOnlyList<{mapping.SourceType}>)(object)source);");
        }

        sb.AppendLine("            throw new NotSupportedException($\"List mapping from {typeof(TSource).Name} to {typeof(TDestination).Name} is not configured.\");");
        sb.AppendLine("        }");

        // Generate MapList<TSource, TDestination>(IEnumerable) - NEW overload
        sb.AppendLine();
        sb.AppendLine("        public List<TDestination> MapList<TSource, TDestination>(System.Collections.Generic.IEnumerable<TSource> source)");
        sb.AppendLine("        {");

        foreach (var mapping in model.Mappings)
        {
            sb.AppendLine($"            if (typeof(TSource) == typeof({mapping.SourceType}) && typeof(TDestination) == typeof({mapping.DestinationType}))");
            sb.AppendLine($"                return (List<TDestination>)(object)MapList_{mapping.SourceTypeName}_To_{mapping.DestinationTypeName}_Enumerable((System.Collections.Generic.IEnumerable<{mapping.SourceType}>)(object)source);");
        }

        sb.AppendLine("            throw new NotSupportedException($\"Enumerable mapping from {typeof(TSource).Name} to {typeof(TDestination).Name} is not configured.\");");
        sb.AppendLine("        }");
    }

    // ============================================================================
    // CODE GENERATION: Extension methods (zero-overhead mapping)
    // ============================================================================

    /// <summary>
    /// Generate specialized extension methods for ZERO overhead mapping IN A SEPARATE FILE.
    ///
    /// These methods bypass the generic dispatch entirely!
    /// Example:
    /// public static Person ToPerson(this PersonDto source) =>
    ///     new Person { PersonId = source.Id, ... };
    ///
    /// PERFORMANCE: This is as fast as manual mapping - no boxing, no typeof checks!
    /// </summary>
    private static string GenerateExtensionMethodsCode(MapperConfigurationModel model)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8601 // Possible null reference assignment in generated mapping code");
        sb.AppendLine();

        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();

        sb.AppendLine($"// Extension methods for zero-overhead mapping");
        sb.AppendLine($"// NOTE: Uses fully qualified type names to avoid namespace conflicts");
        sb.AppendLine($"public static class {model.ClassName}_Extensions");
        sb.AppendLine("{");

        foreach (var mapping in model.Mappings)
        {
            sb.AppendLine();
            sb.AppendLine($"    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static {mapping.DestinationType} To{mapping.DestinationTypeName}(this {mapping.SourceType} source)");
            sb.AppendLine("    {");

            var hasInitOnlyProps = mapping.DestinationProperties.Any(p => p.IsInitOnly || p.IsRequired);
            var hasConditions = mapping.MemberConfigurations.Any(c => !string.IsNullOrEmpty(c.Condition));

            if (hasInitOnlyProps && !hasConditions)
            {
                sb.AppendLine($"        return new {mapping.DestinationType}");
                sb.AppendLine("        {");

                var propsToInitialize = new List<string>();
                foreach (var destProp in mapping.DestinationProperties)
                {
                    var memberConfig = mapping.MemberConfigurations.FirstOrDefault(
                        c => c.DestinationMember == destProp.Name);

                    if (memberConfig?.IsIgnored == true)
                        continue;

                    var sourcePropName = memberConfig?.SourceMember ?? destProp.Name;
                    var sourceProp = mapping.SourceProperties.FirstOrDefault(p => p.Name == sourcePropName);

                    // Complex MapFrom expression takes precedence over simple property mapping
                    if (!string.IsNullOrEmpty(memberConfig?.SourceExpression))
                    {
                        propsToInitialize.Add($"            {destProp.Name} = {memberConfig!.SourceExpression}");
                    }
                    else if (sourceProp != null)
                    {
                        propsToInitialize.Add($"            {destProp.Name} = source.{sourceProp.Name}");
                    }
                }

                for (int i = 0; i < propsToInitialize.Count; i++)
                {
                    if (i < propsToInitialize.Count - 1)
                        sb.AppendLine(propsToInitialize[i] + ",");
                    else
                        sb.AppendLine(propsToInitialize[i]);
                }

                sb.AppendLine("        };");
            }
            else
            {
                sb.AppendLine($"        var destination = new {mapping.DestinationType}();");

                foreach (var destProp in mapping.DestinationProperties)
                {
                    var memberConfig = mapping.MemberConfigurations.FirstOrDefault(
                        c => c.DestinationMember == destProp.Name);

                    if (memberConfig?.IsIgnored == true)
                        continue;

                    var sourcePropName = memberConfig?.SourceMember ?? destProp.Name;
                    var sourceProp = mapping.SourceProperties.FirstOrDefault(p => p.Name == sourcePropName);

                    if (sourceProp != null || !string.IsNullOrEmpty(memberConfig?.SourceExpression))
                    {
                        GeneratePropertyAssignmentForExtension(sb, destProp, sourceProp!, memberConfig, mapping);
                    }
                }

                sb.AppendLine("        return destination;");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");  // Close extension class

        return sb.ToString();
    }

    // ============================================================================
    // CODE GENERATION: Unified Mapper (from profiles)
    // ============================================================================

    /// <summary>
    /// Generate a unified mapper class that combines all mappings from IMapperProfile implementations.
    ///
    /// This is used for DI registration - a single IMapper instance that handles ALL profile mappings.
    ///
    /// For beginners: When you use profiles, each profile defines some mappings.
    /// The unified mapper combines ALL of them into one mapper that can handle everything.
    /// </summary>
    private static string GenerateUnifiedMapperCode(string ns, List<MappingModel> mappings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8601 // Possible null reference assignment in generated mapping code");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using ZMapper;");
        sb.AppendLine("using ZMapper.Abstractions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Unified mapper generated from all IMapperProfile implementations.");
        sb.AppendLine("/// Register via services.AddZMapper() or create manually with Mapper.Create().");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class Mapper : IMapper");
        sb.AppendLine("{");

        // Generate hook fields for all mappings that have hooks
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.BeforeMapFieldName))
            {
                sb.AppendLine($"    internal static System.Action<{mapping.SourceType}, {mapping.DestinationType}>? {mapping.BeforeMapFieldName};");
            }
            if (!string.IsNullOrEmpty(mapping.AfterMapFieldName))
            {
                sb.AppendLine($"    internal static System.Action<{mapping.SourceType}, {mapping.DestinationType}>? {mapping.AfterMapFieldName};");
            }
        }
        if (mappings.Any(m => !string.IsNullOrEmpty(m.BeforeMapFieldName) || !string.IsNullOrEmpty(m.AfterMapFieldName)))
        {
            sb.AppendLine();
        }

        // Generate 5 methods for each mapping
        foreach (var mapping in mappings)
        {
            GenerateMapMethod(sb, mapping);
            GenerateMapToExistingMethod(sb, mapping);
            GenerateMapArrayMethod(sb, mapping);
            GenerateMapListMethod(sb, mapping);
            GenerateMapListEnumerableMethod(sb, mapping);
        }

        // Generate generic dispatch methods
        // Build a temporary model to reuse existing logic
        var tempModel = new MapperConfigurationModel
        {
            Namespace = ns,
            ClassName = "Mapper",
            MethodName = "",
            Mappings = mappings
        };
        GenerateGenericDispatchMethods(sb, tempModel);

        sb.AppendLine();

        // Static factory method: Create(MapperConfiguration config)
        // Wires up hooks from the config to static fields
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Create a unified mapper with hook support from a MapperConfiguration.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static Mapper Create(MapperConfiguration config)");
        sb.AppendLine("    {");

        int hookIdx = 0;
        foreach (var mapping in mappings)
        {
            if (string.IsNullOrEmpty(mapping.BeforeMapFieldName) && string.IsNullOrEmpty(mapping.AfterMapFieldName))
                continue;

            sb.AppendLine($"        var mapping_{mapping.SourceTypeName}_{mapping.DestinationTypeName} = config.Mappings");
            sb.AppendLine($"            .FirstOrDefault(m => m.SourceType == typeof({mapping.SourceType}) && m.DestinationType == typeof({mapping.DestinationType}));");

            if (!string.IsNullOrEmpty(mapping.BeforeMapFieldName))
            {
                sb.AppendLine($"        if (mapping_{mapping.SourceTypeName}_{mapping.DestinationTypeName}?.BeforeMapAction is System.Action<{mapping.SourceType}, {mapping.DestinationType}> beforeMap_{hookIdx})");
                sb.AppendLine($"            {mapping.BeforeMapFieldName} = beforeMap_{hookIdx};");
            }

            if (!string.IsNullOrEmpty(mapping.AfterMapFieldName))
            {
                sb.AppendLine($"        if (mapping_{mapping.SourceTypeName}_{mapping.DestinationTypeName}?.AfterMapAction is System.Action<{mapping.SourceType}, {mapping.DestinationType}> afterMap_{hookIdx})");
                sb.AppendLine($"            {mapping.AfterMapFieldName} = afterMap_{hookIdx};");
            }

            sb.AppendLine();
            hookIdx++;
        }

        sb.AppendLine("        return new Mapper();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Parameterless Create (no hooks)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Create a unified mapper without hooks.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static Mapper Create()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new Mapper();");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ============================================================================
    // CODE GENERATION: DI Extension (AddZMapper)
    // ============================================================================

    /// <summary>
    /// Generate the AddZMapper() service collection extension method.
    ///
    /// This is only generated when the consuming project references
    /// Microsoft.Extensions.DependencyInjection.Abstractions.
    ///
    /// For beginners: This generates the code you call in Program.cs:
    /// builder.Services.AddZMapper();
    /// It registers IMapper in the DI container with all your profile mappings.
    /// </summary>
    private static string GenerateServiceCollectionExtension(
        string ns,
        List<ProfileConfigurationModel?> profiles)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8601 // Possible null reference assignment in generated mapping code");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using ZMapper;");
        sb.AppendLine("using ZMapper.Abstractions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// DI extension for registering ZMapper with all discovered profiles.");
        sb.AppendLine("/// Usage: builder.Services.AddZMapper();");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ZMapper_ServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers IMapper as a singleton with all mappings from IMapperProfile implementations.");
        sb.AppendLine("    /// Profiles are instantiated and their Configure methods called to collect all mappings and hooks.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddZMapper(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Create a shared configuration to collect all profile mappings and hooks");
        sb.AppendLine("        var config = new MapperConfiguration();");
        sb.AppendLine();

        // Instantiate each profile and call Configure
        foreach (var profile in profiles.Where(p => p != null))
        {
            sb.AppendLine($"        // Register mappings from {profile!.ClassName}");
            sb.AppendLine($"        new {profile.FullTypeName}().Configure(config);");
        }

        sb.AppendLine();
        sb.AppendLine("        // Create the unified mapper with all collected mappings and hooks");
        sb.AppendLine("        var mapper = Mapper.Create(config);");
        sb.AppendLine("        services.AddSingleton<IMapper>(mapper);");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

// ============================================================================
// DATA MODELS
// These classes hold information during code generation.
// They're like "notes" we take while analyzing the user's code.
// ============================================================================

/// <summary>
/// Represents all mapper configuration in one class (CreateMap-based pattern).
/// Example: MapperConfig class with multiple CreateMap calls.
/// </summary>
internal class MapperConfigurationModel
{
    /// <summary>Namespace of the mapper class (e.g., "MyApp.Mappers")</summary>
    public string Namespace { get; set; } = "";

    /// <summary>Name of the mapper class (e.g., "MapperConfig")</summary>
    public string ClassName { get; set; } = "";

    /// <summary>Name of the configuration method (e.g., "ConfigureMapper")</summary>
    public string MethodName { get; set; } = "";

    /// <summary>Whether the method takes a MapperConfiguration parameter (for hooks initialization)</summary>
    public bool MethodTakesConfig { get; set; }

    /// <summary>Debug info about parameter type detection</summary>
    public string DebugParamInfo { get; set; } = "";

    /// <summary>All the mappings configured in this class</summary>
    public List<MappingModel> Mappings { get; set; } = new();
}

/// <summary>
/// Represents a class implementing IMapperProfile.
/// Contains mapping information extracted from the Configure method.
/// </summary>
internal class ProfileConfigurationModel
{
    /// <summary>Namespace of the profile class</summary>
    public string Namespace { get; set; } = "";

    /// <summary>Short class name (e.g., "UserProfile")</summary>
    public string ClassName { get; set; } = "";

    /// <summary>Fully qualified type name (e.g., "MyApp.Profiles.UserProfile")</summary>
    public string FullTypeName { get; set; } = "";

    /// <summary>All the mappings configured in this profile's Configure method</summary>
    public List<MappingModel> Mappings { get; set; } = new();
}

/// <summary>
/// Represents one mapping between two types.
/// Example: CreateMap&lt;UserDto, User&gt;()...
/// </summary>
internal class MappingModel
{
    /// <summary>Full source type name with namespace (e.g., "MyApp.UserDto")</summary>
    public string SourceType { get; set; } = "";

    /// <summary>Just the source type name (e.g., "UserDto")</summary>
    public string SourceTypeName { get; set; } = "";

    /// <summary>Full destination type name with namespace</summary>
    public string DestinationType { get; set; } = "";

    /// <summary>Just the destination type name (e.g., "User")</summary>
    public string DestinationTypeName { get; set; } = "";

    /// <summary>All public properties on the source type</summary>
    public List<PropertyModel> SourceProperties { get; set; } = new();

    /// <summary>All public properties on the destination type</summary>
    public List<PropertyModel> DestinationProperties { get; set; } = new();

    /// <summary>Custom configurations (ForMember calls)</summary>
    public List<MemberConfigModel> MemberConfigurations { get; set; } = new();

    /// <summary>Name of BeforeMap action field (if any)</summary>
    public string? BeforeMapFieldName { get; set; }

    /// <summary>Name of AfterMap action field (if any)</summary>
    public string? AfterMapFieldName { get; set; }

    /// <summary>If true, non-matching destination properties are silently skipped.
    /// If false (default), a compile-time diagnostic is emitted for unmapped properties.</summary>
    public bool IgnoreNonExisting { get; set; }
}

/// <summary>
/// Represents one property on a type.
/// Contains everything we need to know to generate mapping code.
/// </summary>
internal class PropertyModel
{
    /// <summary>Property name (e.g., "UserId")</summary>
    public string Name { get; set; } = "";

    /// <summary>Property type (e.g., "int", "string", "List&lt;User&gt;")</summary>
    public string Type { get; set; } = "";

    /// <summary>Is this a 'required' property (C# 11+)?</summary>
    public bool IsRequired { get; set; }

    /// <summary>Is this nullable (string? vs string)?</summary>
    public bool IsNullable { get; set; }

    /// <summary>Is this init-only (set; vs init;)?</summary>
    public bool IsInitOnly { get; set; }

    /// <summary>Is this a complex type (not a primitive/value type)? Used for nested object mapping.</summary>
    public bool IsComplexType { get; set; }

    /// <summary>Is this a collection type (List, Array, IEnumerable)? Used for collection mapping.</summary>
    public bool IsCollection { get; set; }

    /// <summary>Element type for collections (List&lt;T&gt; -> T). Null if not a collection.</summary>
    public string? CollectionElementType { get; set; }
}

/// <summary>
/// Represents one ForMember configuration.
/// Example: .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
/// </summary>
internal class MemberConfigModel
{
    /// <summary>Which destination property is being configured (e.g., "UserId")</summary>
    public string DestinationMember { get; set; } = "";

    /// <summary>Which source property to map from (e.g., "Id"), or null if convention</summary>
    public string? SourceMember { get; set; }

    /// <summary>
    /// Full source expression for complex MapFrom lambdas (e.g., "source.Client.CompanyName"
    /// or "source.IssueDate ?? DateTime.UtcNow"). Used when the lambda body is not a simple
    /// property access. When set, this takes precedence over SourceMember.
    /// </summary>
    public string? SourceExpression { get; set; }

    /// <summary>Should this property be ignored (not mapped)?</summary>
    public bool IsIgnored { get; set; }

    /// <summary>Condition expression for conditional mapping (e.g., "src => src.Value != null")</summary>
    public string? Condition { get; set; }
}
