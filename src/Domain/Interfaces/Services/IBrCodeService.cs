namespace Domain.Interfaces.Services;

public interface IBrCodeService
{
    string GenerateBrCode(string pixKey, decimal amount, string merchantName, string merchantCity, string txId, string? description = null, bool isDynamic = false);
    string GenerateSvg(string payload, int size = 280);
    string ComputeCrc16Hex(string payload);
}

