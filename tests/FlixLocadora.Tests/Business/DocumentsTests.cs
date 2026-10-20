using FlixLocadora.Api.Business.Common;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Business;

public class DocumentsTests
{
    [Theory]
    [InlineData("11222333000181")]
    [InlineData("11.222.333/0001-81")]
    [InlineData("12ABC34501DE35")]          // alphanumeric — the Receita Federal example
    [InlineData("12.abc.345/01de-35")]
    public void Valid_cnpjs(string cnpj) => Assert.True(Cnpj.IsValid(cnpj));

    [Theory]
    [InlineData("11222333000180")]
    [InlineData("11111111111111")]
    [InlineData("12ABC34501DEAB")]
    [InlineData("1122233300018")]
    [InlineData(null)]
    public void Invalid_cnpjs(string? cnpj) => Assert.False(Cnpj.IsValid(cnpj));

    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    public void Valid_cpfs(string cpf) => Assert.True(Cpf.IsValid(cpf));

    [Theory]
    [InlineData("52998224724")]
    [InlineData("11111111111")]
    [InlineData("5299822472")]
    [InlineData("529.982.247/25")]
    [InlineData(null)]
    public void Invalid_cpfs(string? cpf) => Assert.False(Cpf.IsValid(cpf));

    [Fact]
    public void Generated_test_documents_are_valid()
    {
        Assert.True(Cnpj.IsValid(TestData.NewCnpj()));
        Assert.True(Cnpj.IsValid(TestData.NewCnpj(alphanumeric: true)));
        Assert.True(Cpf.IsValid(TestData.NewCpf()));
    }
}
