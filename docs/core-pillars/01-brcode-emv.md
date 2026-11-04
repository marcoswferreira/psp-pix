# Pilar 1: Cobrança Imediata & BR Code EMVCo (Padrão Bacen)

O Pix adota a especificação internacional **EMVCo (Merchant-Presented Mode)** para a codificação de cobranças em QR Codes estáticos e dinâmicos, complementada pelo manual de iniciação do **Banco Central do Brasil (Bacen)**.

---

## 🏷️ Estrutura Tag-Length-Value (TLV)

O payload do BR Code é uma sequência contínua de blocos no formato **TLV**:
* **Tag (ID)**: Identificador do campo com 2 dígitos numéricos (ex: `00`, `26`, `54`).
* **Length (Tamanho)**: Comprimento do valor com 2 dígitos numéricos (ex: `02`, `14`, `06`).
* **Value (Conteúdo)**: O valor literal em formato string ASCII.

### Dicionário de Tags Bacen Pix

| Tag | Nome do Campo | Descrição e Regras | Exemplo Pix |
| :--- | :--- | :--- | :--- |
| **`00`** | Payload Format Indicator | Versão da especificação EMV (fixo em `01`) | `000201` |
| **`01`** | Point of Initiation Method | `11` para QR Code estático ou `12` para QR dinâmico | `010212` |
| **`26`** | Merchant Account Info | Sub-tags com o domínio do Bacen e chave Pix | `26...` |
| `26.00` | *Globally Unique Identifier (GUI)* | Fixo em `br.gov.bcb.pix` | `0014br.gov.bcb.pix` |
| `26.01` | *Chave Pix* | Chave de endereçamento (CPF, CNPJ, E-mail, Fone ou EVP) | `011112345678901` |
| `26.02` | *Descrição adicional* | Texto opcional informativo | `0210PEDIDO 100` |
| **`52`** | Merchant Category Code | Código de categoria do estabelecimento (`0000` padrão Pix) | `52040000` |
| **`53`** | Transaction Currency | Código numérico da moeda ISO 4217 (`986` para Real - BRL) | `5303986` |
| **`54`** | Transaction Amount | Valor da transação com 2 casas decimais (ponto separador) | `5406125.75` |
| **`58`** | Country Code | Código ISO 3166-1 alpha 2 do país (fixo em `BR`) | `5802BR` |
| **`59`** | Merchant Name | Nome do beneficiário (máx. 25 chars, sem acentuação) | `5908LOJA ABC` |
| **`60`** | Merchant City | Cidade do beneficiário (máx. 15 chars, sem acentuação) | `6009SAO PAULO` |
| **`62`** | Additional Data Field | Sub-tags de controle | `62...` |
| `62.05` | *Reference Label (TxId)* | Identificador da transação (1 a 35 caracteres alfanuméricos) | `0516TX12345678901234` |
| **`63`** | CRC16 Checksum | Tag `63` com tamanho `04`, seguida pelos 4 caracteres hex do CRC | `6304D3E6` |

---

## 🧮 Algoritmo de Checksum CRC16-CCITT

O Bacen exige a validação de integridade através do algoritmo **CRC16-CCITT (False)**:
* **Polinômio Gerador**: `0x1021` ($x^{16} + x^{12} + x^5 + 1$)
* **Valor Inicial (*Initial Value*)**: `0xFFFF`
* **XorOut**: `0x0000`
* **Entrada e Saída**: Sem reversão de bits (*Non-reflected*).

### Implementação C# Pura em `BrCodeService`:
```csharp
public string ComputeCrc16Hex(string payload)
{
    ushort crc = 0xFFFF;
    ushort polynomial = 0x1021;
    var bytes = Encoding.UTF8.GetBytes(payload);

    foreach (var b in bytes)
    {
        crc ^= (ushort)(b << 8);
        for (int i = 0; i < 8; i++)
        {
            if ((crc & 0x8000) != 0)
                crc = (ushort)((crc << 1) ^ polynomial);
            else
                crc <<= 1;
        }
    }

    return crc.ToString("X4");
}
```

### Vetor de Teste Oficial Bacen (Golden Vector)
O cálculo foi testado e validado contra a string de teste regulatória do Bacen:
```
Vetor de Entrada:
00020126330014br.gov.bcb.pix0111123456789005204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***6304

Resultado Calculado:
D3E6
```
Nosso teste unitário `ComputeCrc16Hex_WithKnownBacenString_ShouldReturnD3E6` garante conformidade matemática estrita com a especificação oficial.

---

## 🖼️ Geração do SVG do QR Code

Além da string textual do BR Code, a API gera em tempo real a representação vetorial em SVG:
* Sem dependências de bibliotecas externas pesadas ou binários de C++.
* Renderizado com viewBox responsivo e estilo visual conforme a identidade visual do Pix (Badge central `#32BCAD` com texto "PIX").
* Pode ser injetado diretamente no HTML de front-ends ou impresso em notas fiscais e terminais POS.
