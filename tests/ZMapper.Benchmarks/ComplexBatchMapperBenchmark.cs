using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace ZMapper.Benchmarks;

/// <summary>
/// Benchmark for batch mapping of complex objects (arrays/lists of orders).
/// Measures overhead of mapping collections of nested objects - a common real-world scenario
/// where APIs return paginated lists of orders, each with items and status info.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public partial class ComplexBatchMapperBenchmark
{
    // Array of source orders to map
    private ComplexOrder[] _orders = null!;

    // Mapper instances
    private AutoMapper.IMapper _autoMapper = null!;
    private ZMapper.IMapper _zMapper = null!;
    private ComplexMapperlyMapper _mapperlyMapper = null!;

    /// <summary>
    /// Number of orders to map in each benchmark iteration.
    /// 10 = small API response, 100 = large page, 1000 = bulk export.
    /// </summary>
    [Params(10, 100, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Generate realistic test data: each order has 3 items and a status
        _orders = Enumerable.Range(1, Count).Select(i => new ComplexOrder
        {
            Id = i,
            OrderNumber = $"ORD-2024-{i:D5}",
            OrderedAt = DateTime.UtcNow.AddDays(-i),
            ShippedDate = i % 3 == 0 ? new DateOnly(2024, 6, 15 + (i % 15)) : null,
            TotalAmount = 100m + i * 10m,
            ShippingCost = i % 5 == 0 ? 12.99m : null,
            PaymentMethod = (PaymentMethod)(i % 5),
            IsPaid = i % 4 != 0,
            TrackingNumber = i % 2 == 0 ? $"TRK-{i:D10}" : null,
            CustomerId = Guid.NewGuid(),
            CurrentStatus = new ComplexOrderStatusInfo
            {
                Id = i,
                Status = (OrderStatus)(i % 7),
                Note = i % 3 == 0 ? $"Note for order {i}" : null,
                ChangedBy = "System",
                ChangedAt = DateTime.UtcNow.AddHours(-i),
                IsTerminal = i % 7 >= 4 // Delivered, Cancelled, Returned
            },
            Items = Enumerable.Range(1, 3).Select(j => new ComplexOrderItem
            {
                Id = i * 100 + j,
                ProductName = $"Product-{j}",
                ProductCode = j % 2 == 0 ? $"PC-{j}" : null,
                Quantity = j,
                UnitPrice = 10m * j + 0.99m,
                Discount = j == 2 ? 3.00m : null,
                IsGift = j == 3
            }).ToList()
        }).ToArray();

        // Setup AutoMapper
        var autoMapperConfig = new AutoMapper.MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ComplexAddress, ComplexAddressDto>();
            cfg.CreateMap<ComplexCustomer, ComplexCustomerDto>();
            cfg.CreateMap<ComplexOrderStatusInfo, ComplexOrderStatusInfoDto>();
            cfg.CreateMap<ComplexOrderItem, ComplexOrderItemDto>();
            cfg.CreateMap<ComplexOrder, ComplexOrderDto>();
        });
        _autoMapper = autoMapperConfig.CreateMapper();

        // Setup ZMapper
        _zMapper = ZMapperConfig.CreateMapper();

        // Setup Mapperly
        _mapperlyMapper = new ComplexMapperlyMapper();
    }

    // =========================================================================
    // Manual baseline - hand-written loop, theoretical ceiling
    // =========================================================================

    /// <summary>
    /// Baseline: manually map each order in a for-loop.
    /// </summary>
    [Benchmark(Baseline = true)]
    public ComplexOrderDto[] ManualLoop()
    {
        var result = new ComplexOrderDto[_orders.Length];
        for (int i = 0; i < _orders.Length; i++)
        {
            var src = _orders[i];
            var dto = new ComplexOrderDto
            {
                Id = src.Id,
                OrderNumber = src.OrderNumber,
                OrderedAt = src.OrderedAt,
                ShippedDate = src.ShippedDate,
                TotalAmount = src.TotalAmount,
                ShippingCost = src.ShippingCost,
                PaymentMethod = src.PaymentMethod,
                IsPaid = src.IsPaid,
                TrackingNumber = src.TrackingNumber,
                CustomerId = src.CustomerId,
                CurrentStatus = new ComplexOrderStatusInfoDto
                {
                    Id = src.CurrentStatus.Id,
                    Status = src.CurrentStatus.Status,
                    Note = src.CurrentStatus.Note,
                    ChangedBy = src.CurrentStatus.ChangedBy,
                    ChangedAt = src.CurrentStatus.ChangedAt,
                    IsTerminal = src.CurrentStatus.IsTerminal
                }
            };

            var items = new List<ComplexOrderItemDto>(src.Items.Count);
            foreach (var item in src.Items)
            {
                items.Add(new ComplexOrderItemDto
                {
                    Id = item.Id,
                    ProductName = item.ProductName,
                    ProductCode = item.ProductCode,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Discount = item.Discount,
                    TotalPrice = item.TotalPrice,
                    IsGift = item.IsGift
                });
            }
            dto.Items = items;
            result[i] = dto;
        }
        return result;
    }

    // =========================================================================
    // ZMapper benchmarks - Span-based and loop-based
    // =========================================================================

    /// <summary>
    /// ZMapper using MapArray with ReadOnlySpan - tests Span optimization
    /// for complex objects with nested collections.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto[] ZMapperWithSpan()
    {
        return _zMapper.MapArray<ComplexOrder, ComplexOrderDto>(_orders.AsSpan());
    }

    /// <summary>
    /// ZMapper using individual Map calls in a loop.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto[] ZMapperLoop()
    {
        var result = new ComplexOrderDto[_orders.Length];
        for (int i = 0; i < _orders.Length; i++)
        {
            result[i] = _zMapper.Map<ComplexOrder, ComplexOrderDto>(_orders[i]);
        }
        return result;
    }

    // =========================================================================
    // Mapperly benchmark
    // =========================================================================

    /// <summary>
    /// Mapperly source-generated mapping in a loop.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto[] MapperlyLoop()
    {
        var result = new ComplexOrderDto[_orders.Length];
        for (int i = 0; i < _orders.Length; i++)
        {
            result[i] = _mapperlyMapper.MapOrder(_orders[i]);
        }
        return result;
    }

    // =========================================================================
    // AutoMapper benchmarks - List and loop variants
    // =========================================================================

    /// <summary>
    /// AutoMapper using built-in collection mapping (Map to List).
    /// </summary>
    [Benchmark]
    public List<ComplexOrderDto> AutoMapperList()
    {
        return _autoMapper.Map<List<ComplexOrderDto>>(_orders);
    }

    /// <summary>
    /// AutoMapper using individual Map calls in a loop.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto[] AutoMapperLoop()
    {
        var result = new ComplexOrderDto[_orders.Length];
        for (int i = 0; i < _orders.Length; i++)
        {
            result[i] = _autoMapper.Map<ComplexOrderDto>(_orders[i]);
        }
        return result;
    }
}
