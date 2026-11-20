using Districauchos.Core.Orders;
using Districauchos.Core.Pricing;

namespace Districauchos.Core.Tests;

public class OrderTests
{
    private static CustomerInput Input(string doc = "cedula", string? num = "1710034065", string phone = "0987654321",
        string? address = "Av. Siempre Viva 123", string? city = null) =>
        new("Juan Pérez", doc, num, phone, "Juan@Correo.com", address, city, null);

    [Theory]
    [InlineData("1710034065", true)]
    [InlineData("1710034064", false)]   // dígito verificador
    [InlineData("2510034065", false)]   // provincia 25 no existe
    [InlineData("171003406", false)]
    public void Cedula(string s, bool ok) => Assert.Equal(ok, CustomerValidator.IsValidCedula(s));

    [Theory]
    [InlineData("1710034065001", true)]  // persona natural
    [InlineData("1790011674001", true)]  // sociedad: la valida el SRI
    [InlineData("1710034065000", false)]
    [InlineData("1710034064001", false)]
    public void Ruc(string s, bool ok) => Assert.Equal(ok, CustomerValidator.IsValidRuc(s));

    [Theory]
    [InlineData("0987654321", "+593987654321")]
    [InlineData("098 765 4321", "+593987654321")]
    [InlineData("+593 98 765 4321", "+593987654321")]
    [InlineData("593987654321", "+593987654321")]
    [InlineData("12345", null)]
    [InlineData("022345678", null)]       // fijo sin código: no es celular
    public void Phone(string raw, string? expected) => Assert.Equal(expected, CustomerValidator.NormalizePhone(raw));

    [Fact]
    public void Valid_customer_is_normalized()
    {
        var (c, errors) = CustomerValidator.Validate(Input(), Delivery.Quito);
        Assert.Empty(errors);
        Assert.Equal(("juan@correo.com", "+593987654321", "Quito", DocType.Cedula), (c!.Email, c.Phone, c.City, c.DocType));
    }

    [Fact]
    public void National_needs_address_and_city()
    {
        var (_, errors) = CustomerValidator.Validate(Input(address: "", city: ""), Delivery.National);
        Assert.Contains("address", errors.Keys);
        Assert.Contains("city", errors.Keys);
    }

    [Fact]
    public void Pickup_does_not_need_address()
    {
        var (c, errors) = CustomerValidator.Validate(Input(address: null), Delivery.Pickup);
        Assert.Empty(errors);
        Assert.Null(c!.Address);
    }

    [Theory]
    [InlineData("consumidor_final")]
    [InlineData("")]
    public void Document_is_always_required(string docType) =>
        Assert.Contains("docType", CustomerValidator.Validate(Input(docType, null), Delivery.Pickup).Errors.Keys);

    [Theory]
    [InlineData(10.20, 1020, 887, 133)]
    [InlineData(39.00, 3900, 3391, 509)]
    [InlineData(0.01, 1, 1, 0)]
    public void Amounts_split_iva_and_add_up(decimal total, int amount, int withTax, int tax)
    {
        var a = PaymentAmounts.FromTotal(total, 0.15m);
        Assert.Equal((amount, withTax, tax), (a.Amount, a.AmountWithTax, a.Tax));
        Assert.Equal(a.Amount, a.AmountWithTax + a.Tax);
    }

    [Fact]
    public void Store_message_has_invoice_data_and_backorder()
    {
        var (c, _) = CustomerValidator.Validate(Input(), Delivery.Quito);
        var o = new Order(1001, "tok", OrderStatus.Paid, DateTimeOffset.UnixEpoch,
            c! with { City = "Ambato", Address = "Calle 1" }, Delivery.National,
            [new("0682561", "A1", "Base motor Chevrolet Corsa", 2, 17m, 34m, false, Backorder: true, false)],
            34m, 5m, 39m, false, "Chevrolet Corsa", 4, 123456, "AUT1");
        var text = OrderText.ForStore(o);
        Assert.Contains("PEDIDO #1001 – PAGADO", text);
        Assert.Contains("[BAJO PEDIDO ~4 días]", text);
        Assert.Contains("TOTAL PAGADO: $39.00", text);
        Assert.Contains("Cédula: 1710034065", text);
        Assert.Contains("Envío nacional por cooperativa o Servientrega a Ambato", text);
    }
}
