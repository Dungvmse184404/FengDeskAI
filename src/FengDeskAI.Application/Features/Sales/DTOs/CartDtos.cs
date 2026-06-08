namespace FengDeskAI.Application.Features.Sales.DTOs;

public class CartItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductItemId { get; set; }
    public string? ProductName { get; set; }
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int Stock { get; set; }
    public decimal LineTotal { get; set; }
}

public class CartResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public List<CartItemResponse> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
}

public class AddCartItemRequest
{
    public Guid ProductItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}
