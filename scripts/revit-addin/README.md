# Add-in do portfólio para Revit

## Ambiente de compilação

- Revit 2027 e suas assemblies `RevitAPI` e `RevitAPIUI`.
- SDK do .NET 10; o projeto usa `net10.0-windows`.
- O projeto C# está em [`PortfolioExpansion.csproj`](PortfolioExpansion.csproj). As referências Autodesk usam o caminho padrão da instalação Revit 2027; ajuste o projeto se a instalação local estiver em outro diretório.

Na raiz do repositório, compile com:

```powershell
dotnet build .\scripts\revit-addin\PortfolioExpansion.csproj --configuration Release
```

O build gera o add-in, mas não demonstra por si só que o Revit abriu o modelo, que as vistas foram renderizadas ou que os conectores representam circuitos elétricos.

## Evidência atual — Projeto 19 R20-23

A revisão mais recente gerou e extraiu a candidata R20-23 no Revit 2027.3. A auditoria registrou 168 instâncias, 46 equipamentos elétricos, 24 DirectShapes, 8 CableTray, 25 conectores MEP (dois ligados no sistema auxiliar), 14 folhas, 4 vistas 3D e zero avisos do documento. O PDF nativo do Revit não foi exportado; foi usado um fluxo alternativo de impressão e normalização A3. O canvas permaneceu em branco, portanto não se afirma inspeção visual direta dentro da interface.

Os 12 trajetos e as barras GIS são geometria de estudo. A rede primária não contém condutores ou circuitos nativos conectados. Consulte o [README do Projeto 19](../../projetos/19-subestacao-gis/README.md) e o [comparativo R8 × R20-23](../../projetos/19-subestacao-gis/attempts/r20-23-candidate/dados/reconstrucao-r20/comparativo-r8-r20-23.md) para arquivos, resultados e limites.

## Limitações

Os resultados são demonstrativos e não comprovam projeto executivo, dimensionamento, conformidade normativa, segurança, cabo físico ou conectividade completa. As versões anteriores e seus arquivos permanecem preservados nas pastas de revisão do Projeto 19.
