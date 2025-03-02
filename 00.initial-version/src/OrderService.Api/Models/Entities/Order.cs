namespace OrderService.Api.Models.Entities;
public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; }
    public string CustomerAddress { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerPhone { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItem> OrderItems { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}