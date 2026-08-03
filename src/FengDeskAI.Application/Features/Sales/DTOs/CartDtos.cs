namespace FengDeskAI.Application.Features.Sales.DTOs;

public class CartItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductItemId { get; set; }

    /// <summary>Id sản phẩm gốc (Product) của biến thể — FE dùng để mở trang sản phẩm.</summary>
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? VariantName { get; set; }

    /// <summary>Ảnh đại diện của sản phẩm (SortOrder nhỏ nhất). Null khi sản phẩm chưa có ảnh.</summary>
    public string? ImageUrl { get; set; }
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
