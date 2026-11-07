# Gerador Revit 2027 do portfólio

Projeto C# para a API do Revit 2027. O alvo é .NET 10, de acordo com a documentação oficial de migração da Autodesk: https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm

## Compilação local

1. Confirme que o Revit 2027 e suas DLLs de API estão instalados nos caminhos indicados no `PortfolioExpansion.csproj`.
2. Use o SDK .NET 10 exigido pelo Revit 2027 para compilar `PortfolioExpansion.csproj`.
3. Instale o manifesto `.addin` local apontando para `bin/PortfolioExpansion.dll` e confira `batch-range.txt`.
4. O complemento usa o evento Idling para executar o lote uma vez ao abrir o Revit; remova o manifesto após a geração se não quiser disparo automático em inicializações futuras.

## Projeto 19

O gerador cria paredes nativas do Revit e DirectShapes classificados em Electrical Equipment e Generic Models. Os objetos elétricos são volumes de estudo marcados e comentados, sem conectores MEP, família de fabricante ou circuitos funcionais. Exporta oito folhas A3 e uma vista 3D; o resultado só é válido depois de confirmar o log, abrir o RVT e inspecionar o PDF e os PNGs.

O campo `batch-range.txt` contém `19` para limitar esta execução ao piloto. O histórico dos projetos 01–14 não é reescrito.
