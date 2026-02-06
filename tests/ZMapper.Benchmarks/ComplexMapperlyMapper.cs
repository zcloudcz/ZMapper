using Riok.Mapperly.Abstractions;

namespace ZMapper.Benchmarks;

/// <summary>
/// Mapperly-based mapper for complex types.
/// Mapperly generates mapping code at compile time using source generators,
/// similar to ZMapper. This provides a fair comparison for nested object mapping.
/// </summary>
[Mapper]
public partial class ComplexMapperlyMapper
{
    // Address mapping - straightforward 1:1 property mapping
    public partial ComplexAddressDto MapAddress(ComplexAddress source);

    // Customer mapping - includes nested Address objects
    public partial ComplexCustomerDto MapCustomer(ComplexCustomer source);

    // Order status mapping - includes enum (OrderStatus)
    public partial ComplexOrderStatusInfoDto MapOrderStatus(ComplexOrderStatusInfo source);

    // Order item mapping - Mapperly handles the calculated TotalPrice property
    public partial ComplexOrderItemDto MapOrderItem(ComplexOrderItem source);

    // Full order mapping - nested status + collection of items
    public partial ComplexOrderDto MapOrder(ComplexOrder source);
}
