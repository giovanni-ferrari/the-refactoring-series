using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Models.Entities;
using Microsoft.Data.SqlClient;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly string connectionString;

    public OrderController(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    [HttpGet]
    public IActionResult GetOrders()
    {
        var orders = new List<Order>();
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var command = new SqlCommand("SELECT * FROM [Order].Orders", connection);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    orders.Add(new Order
                    {
                        Id = reader.GetInt32(0),
                        OrderNumber = reader.GetString(1),
                        OrderDate = reader.GetDateTime(2),
                        CustomerName = reader.GetString(3),
                        CustomerAddress = reader.GetString(4),
                        CustomerEmail = reader.GetString(5),
                        CustomerPhone = reader.GetString(6),
                        Status = (OrderStatus)reader.GetInt32(7),
                        // Assuming OrderItems is handled separately
                    });
                }
            }
        }
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public IActionResult GetOrder(int id)
    {
        Order? order = null;
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var command = new SqlCommand("SELECT * FROM [Order].Orders WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    order = new Order
                    {
                        Id = reader.GetInt32(0),
                        OrderNumber = reader.GetString(1),
                        OrderDate = reader.GetDateTime(2),
                        CustomerName = reader.GetString(3),
                        CustomerAddress = reader.GetString(4),
                        CustomerEmail = reader.GetString(5),
                        CustomerPhone = reader.GetString(6),
                        Status = (OrderStatus)reader.GetInt32(7),
                        OrderItems = new List<OrderItem>()
                    };
                }
            }
        }

        if (order != null)
        {
            order.OrderItems = GetOrderItems(order.Id);
        }

        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    [HttpPost]
    public IActionResult CreateOrder([FromBody] Order order)
    {
        if (order == null)
        {
            return BadRequest("Order cannot be null");
        }

        if (order.Status != OrderStatus.Pending)
        {
            return BadRequest("New order must have status of Pending");
        }

        if (order.OrderItems == null || order.OrderItems.Count == 0)
        {
            return BadRequest("Order must have at least one item");
        }

        if (order.OrderItems.Any(x => x.Quantity <= 0))
        {
            return BadRequest("Quantity of each item must be greater than 0");
        }

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var command = new SqlCommand(
                "INSERT INTO [Order].Orders (OrderNumber, OrderDate, CustomerName, CustomerAddress, CustomerEmail, CustomerPhone, Status) " +
                "VALUES (@OrderNumber, @OrderDate, @CustomerName, @CustomerAddress, @CustomerEmail, @CustomerPhone, @Status); " +
                "SELECT SCOPE_IDENTITY();", connection);
            command.Parameters.AddWithValue("@OrderNumber", order.OrderNumber);
            command.Parameters.AddWithValue("@OrderDate", order.OrderDate);
            command.Parameters.AddWithValue("@CustomerName", order.CustomerName);
            command.Parameters.AddWithValue("@CustomerAddress", order.CustomerAddress);
            command.Parameters.AddWithValue("@CustomerEmail", order.CustomerEmail);
            command.Parameters.AddWithValue("@CustomerPhone", order.CustomerPhone);
            command.Parameters.AddWithValue("@Status", (int)order.Status);
            order.Id = Convert.ToInt32(command.ExecuteScalar());

            foreach (var item in order.OrderItems)
            {
                var itemCommand = new SqlCommand(
                    "INSERT INTO [Order].OrderItems (OrderId, ProductCode, ProductName, Quantity) " +
                    "VALUES (@OrderId, @ProductCode, @ProductName, @Quantity);", connection);
                itemCommand.Parameters.AddWithValue("@OrderId", order.Id);
                itemCommand.Parameters.AddWithValue("@ProductCode", item.ProductCode);
                itemCommand.Parameters.AddWithValue("@ProductName", item.ProductName);
                itemCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                itemCommand.ExecuteNonQuery();
            }
        }
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateOrder(int id, [FromBody] Order order)
    {
        if (order == null)
        {
            return BadRequest("Order cannot be null");
        }

        if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
        {
            //Processing, Shipped, Delivered and Cancelled are not allowed to be updated via controller
            return BadRequest("Order status cannot be Processing, Shipped, or Delivered");
        }

        Order? orderOnDatabase = null;

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var command = new SqlCommand("SELECT * FROM [Order].Orders WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    orderOnDatabase = new Order
                    {
                        Id = reader.GetInt32(0),
                        OrderNumber = reader.GetString(1),
                        OrderDate = reader.GetDateTime(2),
                        CustomerName = reader.GetString(3),
                        CustomerAddress = reader.GetString(4),
                        CustomerEmail = reader.GetString(5),
                        CustomerPhone = reader.GetString(6),
                        Status = (OrderStatus)reader.GetInt32(7),
                    };
                }
            }
        }

        if ((orderOnDatabase?.Status == OrderStatus.Shipped 
            || orderOnDatabase?.Status == OrderStatus.Delivered)
            && order.Status == OrderStatus.Cancelled)
        {
            return BadRequest("Cannot cancel an order that has been shipped or delivered");
        }    

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var command = new SqlCommand(
                "UPDATE [Order].Orders SET OrderNumber = @OrderNumber, OrderDate = @OrderDate, CustomerName = @CustomerName, " +
                "CustomerAddress = @CustomerAddress, CustomerEmail = @CustomerEmail, CustomerPhone = @CustomerPhone, Status = @Status " +
                "WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@OrderNumber", order.OrderNumber);
            command.Parameters.AddWithValue("@OrderDate", order.OrderDate);
            command.Parameters.AddWithValue("@CustomerName", order.CustomerName);
            command.Parameters.AddWithValue("@CustomerAddress", order.CustomerAddress);
            command.Parameters.AddWithValue("@CustomerEmail", order.CustomerEmail);
            command.Parameters.AddWithValue("@CustomerPhone", order.CustomerPhone);
            command.Parameters.AddWithValue("@Status", (int)order.Status);
            command.ExecuteNonQuery();

            var deleteItemsCommand = new SqlCommand("DELETE FROM [Order].OrderItems WHERE OrderId = @OrderId", connection);
            deleteItemsCommand.Parameters.AddWithValue("@OrderId", id);
            deleteItemsCommand.ExecuteNonQuery();

            foreach (var item in order.OrderItems)
            {
                var itemCommand = new SqlCommand(
                    "INSERT INTO [Order].OrderItems (OrderId, ProductCode, ProductName, Quantity) " +
                    "VALUES (@OrderId, @ProductCode, @ProductName, @Quantity);", connection);
                itemCommand.Parameters.AddWithValue("@OrderId", id);
                itemCommand.Parameters.AddWithValue("@ProductCode", item.ProductCode);
                itemCommand.Parameters.AddWithValue("@ProductName", item.ProductName);
                itemCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                itemCommand.ExecuteNonQuery();
            }
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteOrder(int id)
    {
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            
            var deleteItemsCommand = new SqlCommand("DELETE FROM [Order].OrderItems WHERE OrderId = @OrderId", connection);
            deleteItemsCommand.Parameters.AddWithValue("@OrderId", id);
            deleteItemsCommand.ExecuteNonQuery();

            var command = new SqlCommand("DELETE FROM [Order].Orders WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }
        return NoContent();
    }

    private List<OrderItem> GetOrderItems(int orderId)
    {
        var items = new List<OrderItem>();
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var command = new SqlCommand("SELECT * FROM [Order].OrderItems WHERE OrderId = @OrderId", connection);
            command.Parameters.AddWithValue("@OrderId", orderId);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    items.Add(new OrderItem
                    {
                        Id = reader.GetInt32(0),
                        OrderId = reader.GetInt32(1),
                        ProductCode = reader.GetString(2),
                        ProductName = reader.GetString(3),
                        Quantity = reader.GetInt32(4),
                    });
                }
            }
        }
        return items;
    }
}
