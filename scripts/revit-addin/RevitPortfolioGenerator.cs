using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace PortfolioCasaRevit
{
    public sealed class RevitPortfolioGenerator : IExternalApplication
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(RevitPortfolioGenerator).Assembly.Location), "..", "..", ".."));
        private const string TemplatePath = @"C:\ProgramData\Autodesk\RVT 2027\Templates\Portuguese\DefaultBRAPTB.rte";
        private const string TitleBlockTemplate = @"C:\ProgramData\Autodesk\RVT 2027\Family Templates\Portuguese\Blocos de margens e carimbo\A3 métrico.rft";
        private static readonly string BatchLog = Path.Combine(Root, "scripts", "revit-addin", "revit-batch.log");
        private static readonly string BatchRange = Path.Combine(Root, "scripts", "revit-addin", "batch-range.txt");
        private static bool _started;
        private static string _wallAuditProject = "";
        private static readonly Dictionary<long,string> _wallAuditDescriptions = new Dictionary<long,string>();

        public Result OnStartup(UIControlledApplication application)
        {
            Log("BATCH_ADDIN_LOADED " + DateTime.Now.ToString("O"));
            application.Idling += OnIdling;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.Idling -= OnIdling;
            return Result.Succeeded;
        }

        private void OnIdling(object sender, IdlingEventArgs args)
        {
            if (_started) return;
            UIApplication uiApp = sender as UIApplication;
            if (uiApp == null) return;
            _started = true;
            uiApp.Idling -= OnIdling;
            var selectedProjects = new HashSet<int>(Enumerable.Range(1, 34));
            try
            {
                var requested = new HashSet<int>();
                string rangeText = File.ReadAllText(BatchRange).Trim();
                foreach (string entry in rangeText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] bounds = entry.Trim().Split('-');
                    int first = int.Parse(bounds[0]);
                    int last = bounds.Length > 1 ? int.Parse(bounds[1]) : first;
                    for (int number = Math.Min(first, last); number <= Math.Max(first, last); number++)
                        if (number >= 1 && number <= 34) requested.Add(number);
                }
                if (requested.Count > 0) selectedProjects = requested;
            }
            catch { }
            Log("BATCH_PROJECTS " + string.Join(",", selectedProjects.OrderBy(x => x)));
            foreach (ProjectSpec spec in GetSpecs().Where(x => selectedProjects.Contains(x.Number)))
            {
                try { BuildOne(uiApp, spec); }
                catch (Exception ex) { _wallAuditProject = ""; Log("PROJECT_FAILED " + spec.Number.ToString("00") + " " + ex); }
            }
            Log("BATCH_FINISHED " + DateTime.Now.ToString("O"));
        }

        private static void BuildOne(UIApplication uiApp, ProjectSpec s)
        {
            string number = s.Number.ToString("00");
            string baseName = number == "01" ? "01-casa-unifamiliar-eletrica" : number + "-" + s.Slug;
            string outputFolder = number == "01" ? "01-casa-unifamiliar-revit" : baseName + "-revit";
            string outDir = number == "19" ? Path.Combine(Root, "projetos", "19-subestacao-gis") : Path.Combine(Root, "outputs", outputFolder);
            string modelPath = Path.Combine(outDir, "revit", baseName + ".rvt");
            string pdfPath = Path.Combine(outDir, "pranchas", baseName + ".pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath));
            WriteFiles(s, outDir, baseName);
            Log("PROJECT_START " + number + " " + s.Title);
            Document doc = uiApp.Application.NewProjectDocument(TemplatePath);
            var sheetIds = new List<ElementId>();
            View3D axon = null;
            using (Transaction tx = new Transaction(doc, "Portfolio BIM - " + s.Title))
            {
                tx.Start();
                _wallAuditProject = number == "07" || number == "09" ? number : "";
                _wallAuditDescriptions.Clear();
                FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new PortfolioFailureAudit(number));
                failureOptions.SetClearAfterRollback(true);
                tx.SetFailureHandlingOptions(failureOptions);
                doc.ProjectInformation.Name = s.Title + " - Estudo BIM";
                doc.ProjectInformation.Number = "PRJ-BIM-" + number;
                doc.ProjectInformation.ClientName = "Estudo demonstrativo";
                WallType sourceWallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First(x => x.Kind == WallKind.Basic);
                FloorType sourceFloorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();
                Color facadeColor = PaletteColor(s.Number);
                Material facadeMaterial = GetOrCreateMaterial(doc, "BIM-" + number + "-Fachada", facadeColor);
                Material roofMaterial = GetOrCreateMaterial(doc, "BIM-" + number + "-Cobertura", Darken(facadeColor, 0.72));
                Material glassMaterial = GetOrCreateMaterial(doc, "BIM-" + number + "-Vidro", new Color(47, 91, 116));
                glassMaterial.Transparency = 14;
                Material metalMaterial = GetOrCreateMaterial(doc, "BIM-" + number + "-Metal", new Color(58, 68, 79));
                Material equipmentMaterial = GetOrCreateMaterial(doc, "BIM-" + number + "-Equipamentos", Lighten(facadeColor, 0.26));
                Material solarMaterial = GetOrCreateMaterial(doc, "BIM-" + number + "-Modulos FV", new Color(37, 86, 137));
                WallType wallType = MaterializeHostType(doc, sourceWallType, "BIM " + number + " - Fachada", facadeMaterial);
                FloorType floorType = MaterializeHostType(doc, sourceFloorType, "BIM " + number + " - Piso", GetOrCreateMaterial(doc, "BIM-" + number + "-Piso", Lighten(facadeColor, 0.84)));
                var levels = new List<Level>();
                for (int f = 0; f < s.Floors; f++)
                {
                    Level level = FindOrCreateLevel(doc, f * 3.6, "0" + (f + 1) + " - " + (f == 0 ? "Acesso" : (s.Floors > 2 ? "Pavimento tipo " + (f + 1) : "Pavimento superior")) + " - +" + (f * 3.6).ToString("0.00") + " m");
                    levels.Add(level);
                    Floor.Create(doc, new List<CurveLoop> { RectangleLoop(0, 0, s.Width, s.Depth, level.Elevation) }, floorType.Id, level.Id);
                    if (s.Enclosed) BuildShell(doc, wallType, level, s, f == 0);
                }
                foreach (Segment seg in s.Partitions)
                {
                    int first = seg.Floor < 0 ? 0 : seg.Floor;
                    int last = seg.Floor < 0 || (seg.Floor == 1 && s.Floors > 2) ? s.Floors - 1 : seg.Floor;
                    for (int f = first; f <= last && f < s.Floors; f++)
                        AddWall(doc, wallType, levels[f], seg.X1, seg.Y1, seg.X2, seg.Y2, M(Math.Min(3.25, WallHeight(s) - 0.3)));
                }
                _wallAuditProject = "";
                if (s.Number == 19)
                    BuildGISPhysicalLayout(doc, levels[0], wallType, equipmentMaterial, metalMaterial, glassMaterial);
                else
                    foreach (Equipment e in s.Equipment)
                    {
                        int f = Math.Min(e.Floor, s.Floors - 1);
                        double z = f * 3.6 + 0.03;
                        if ((s.Number == 11 || s.Number == 14) && e.Name.StartsWith("Mesa FV", StringComparison.OrdinalIgnoreCase))
                            AddSolarTable(doc, e, z, s.Number == 11 ? 0.95 : 0.52, solarMaterial, metalMaterial);
                        else if (s.Number == 13 && (e.Name.StartsWith("Tanque", StringComparison.OrdinalIgnoreCase) || e.Name.StartsWith("Reservatorio", StringComparison.OrdinalIgnoreCase)))
                            AddCylinderSolid(doc, e.Name, e.X, e.Y, e.W, e.D, z, e.H, equipmentMaterial);
                        else
                            AddProjectEquipment(doc, s, e, z, equipmentMaterial, metalMaterial);
                    }
                if (s.Enclosed)
                {
                    CreateRoof(doc, levels[levels.Count - 1], s, roofMaterial, facadeMaterial, glassMaterial, metalMaterial);
                    AddBuildingFeatures(doc, s, facadeMaterial, roofMaterial, glassMaterial, metalMaterial, equipmentMaterial);
                }
                else if (s.Number != 19) AddSiteShelters(doc, s, roofMaterial, facadeMaterial, glassMaterial, metalMaterial, solarMaterial);
                doc.Regenerate();

                ElementId text = GetTextTypeId(doc);
                GraphicsStyle lightStyle = GetLineStyle(doc, "E-ILUMINACAO", new Color(205, 126, 32), 4);
                GraphicsStyle powerStyle = GetLineStyle(doc, "E-FORCA", new Color(38, 91, 158), 4);
                GraphicsStyle dataStyle = GetLineStyle(doc, "E-DADOS", new Color(38, 133, 102), 4);
                GraphicsStyle noteStyle = GetLineStyle(doc, "BIM-ANOTACOES", new Color(53, 59, 66), 2);
                int scale = PlanScale(s.Width, s.Depth);
                ViewPlan ground = CreatePlan(doc, levels[0], "A-01 - " + (s.Enclosed ? "Pavimento de acesso" : "Implantacao geral") + " - " + s.Title, s.Width, s.Depth, scale);
                View secondPlan = s.Number == 19
                    ? (View)CreateDraftingView(doc, "A-02 - Arranjo de zonas e equipamentos GIS")
                    : s.Floors > 1
                        ? (View)CreatePlan(doc, levels[1], "A-02 - " + (s.Floors > 2 ? "Pavimento tipo" : "Pavimento superior") + " - " + s.Title, s.Width, s.Depth, scale)
                        : (View)CreatePlan(doc, levels[0], "A-02 - Zonas e equipamentos - " + s.Title, s.Width, s.Depth, scale);
                View lighting = s.Number == 19
                    ? (View)CreateDraftingView(doc, "E-01 - Iluminacao e zonas GIS")
                    : (View)CreatePlan(doc, levels[0], "E-01 - Iluminacao - " + s.Title, s.Width, s.Depth, scale);
                View power = s.Number == 19
                    ? (View)CreateDraftingView(doc, "E-02 - Forca e sistemas GIS")
                    : (View)CreatePlan(doc, levels[0], "E-02 - Forca e sistemas - " + s.Title, s.Width, s.Depth, scale);
                ViewDrafting distribution = CreateDraftingView(doc, s.Number == 19 ? "E-03 - Unifilar 138 e 13,8 kV - GIS" : "E-03 - Diagrama funcional - " + s.Title);
                axon = Create3DView(doc, "BIM-01 - Axonometria - " + s.Title, s);

                DrawSpaces(doc, ground, text, s.Spaces.Where(x => x.Floor == 0), 0.0);
                DrawFurniturePlan(doc, ground, text, s, 0);
                if (s.Number == 19)
                    DrawGISEquipmentPlan(doc, (ViewDrafting)secondPlan, text, noteStyle, powerStyle);
                else if (s.Floors > 1) DrawSpaces(doc, secondPlan, text, s.Spaces.Where(x => x.Floor == 1), levels[1].Elevation);
                else
                {
                    List<Equipment> planEquipment = s.Equipment.Where(x => x.Floor == 0).ToList();
                    DrawSpaces(doc, secondPlan, text, s.Spaces.Where(x => x.Floor == 0 &&
                        !IsEquipmentLabelZone(x, planEquipment) &&
                        !(s.Number == 10 && x.Name.StartsWith("SETOR ", StringComparison.OrdinalIgnoreCase)) &&
                        !(s.Number == 11 && x.Name.StartsWith("SETOR FV ", StringComparison.OrdinalIgnoreCase))), 0.0);
                    DrawEquipmentPlan(doc, secondPlan, text, noteStyle, s, 0.0);
                }
                if (s.Floors > 1 || s.Number == 10)
                    DrawFurniturePlan(doc, secondPlan, text, s, s.Floors > 1 ? 1 : 0);
                if (s.Number == 19)
                {
                    DrawGISSystemPlan(doc, (ViewDrafting)lighting, text, lightStyle, noteStyle, true);
                    DrawGISSystemPlan(doc, (ViewDrafting)power, text, powerStyle, noteStyle, false);
                }
                else
                {
                    DrawSystemPlan(doc, lighting, text, s, lightStyle, true, levels[0].Elevation);
                    DrawSystemPlan(doc, power, text, s, powerStyle, false, levels[0].Elevation);
                }
                DrawDistribution(doc, distribution, text, s, noteStyle, lightStyle, powerStyle, dataStyle);

                ElementId titleBlock = EnsureTitleBlock(doc, uiApp.Application, outDir);
                sheetIds.Add(CreateSheet(doc, titleBlock, "A-01", s.Enclosed ? "Planta de acesso" : "Implantacao geral", ground, text,
                    "PROGRAMA\n" + s.Program + "\n\nDIMENSOES DE ESTUDO\n" + s.Width.ToString("0.00") + " x " + s.Depth.ToString("0.00") + " m por nivel\nNiveis modelados: " + s.Floors + "\n\nNOTA\nGeometria e compartimentacao conceituais." , s, baseName));
                sheetIds.Add(CreateSheet(doc, titleBlock, "A-02", s.Floors > 1 ? "Pavimento superior / tipo" : "Zonas e equipamentos", secondPlan, text,
                    s.Floors > 1 ? "ORGANIZACAO\n" + s.UpperProgram + "\n\nALTURA ENTRE NIVEIS\n3,60 m - premissa de estudo.\n\nNOTA\nCirculacao e requisitos legais nao verificados." : "LEITURA DO PROGRAMA\n" + s.Program + "\n\nETIQUETAS DE EQUIPAMENTOS\n" + (s.Number == 19 ? BuildGISEquipmentLegend() : BuildEquipmentLegend(s)) + "\n\nNOTA\nPosicoes e dimensoes esquematicas; volumes de referencia.", s, baseName));
                sheetIds.Add(CreateSheet(doc, titleBlock, "E-01", "Iluminacao e zonas", lighting, text,
                    "LEGENDA\nCirculo e etiqueta: ponto de luz esquematico\n\nCRITERIO\n" + s.LightNote + "\n\nLIMITACAO\nSem calculo luminotecnico ou fotometria.", s, baseName));
                sheetIds.Add(CreateSheet(doc, titleBlock, "E-02", "Forca e sistemas", power, text,
                    "LEGENDA\nAzul: ponto de forca / carga\nVerde: ponto de dados ou sistema\nQD-01: quadro de referencia\n\nCRITERIO\n" + s.PowerNote + "\n\nLIMITACAO\nAnotacoes esquematicas, sem circuitos MEP conectados.", s, baseName));
                sheetIds.Add(CreateSheet(doc, titleBlock, "E-03", s.Number == 19 ? "Unifilar 138 / 13,8 kV" : "Diagrama funcional", distribution, text,
                    s.Number == 19 ? "ARRANJO FUNCIONAL\nRede 138 kV -> bays GIS -> TR-01/TR-02 -> barras 13,8 kV A/B.\n\nLIMITES\nSem dados de rede, curvas ou ajustes." : "ORGANIZACAO\nEntrada de estudo -> quadro geral\nDistribuicao setorizada por uso.\n\nSISTEMAS\n" + string.Join("\n", s.Systems.Take(5)) + "\n\nAVISO\nSem demanda, dimensionamento ou coordenacao de protecoes.", s, baseName));
                sheetIds.Add(CreateSheet(doc, titleBlock, "BIM-01", "Axonometria e criterios", axon, text,
                    "MODELO\n" + s.ModelDescription + "\n\nBIM\nModelo nativo Revit 2027. " + (s.Number == 19 ? "Paredes nativas e DirectShape categorizados; sem conectores ou circuitos MEP." : "Instalacoes representadas por anotacoes 2D.") + "\n\nUSO\nEstudo demonstrativo - nao executivo.", s, baseName));
                if (s.Number == 19)
                {
                    ViewDrafting protection = CreateDraftingView(doc, "E-04 - Protecao, controle e servicos auxiliares - GIS");
                    DrawGISProtectionArchitecture(doc, protection, text, noteStyle, powerStyle, dataStyle);
                    sheetIds.Add(CreateSheet(doc, titleBlock, "E-04", "Proteção, controle e auxiliares", protection, text,
                        "ARQUITETURA FUNCIONAL\nIEDs, HMI, gateway, rede e CC.\n\nLIMITES\nSem fabricante, ajustes, autonomia ou estudo de seletividade.", s, baseName));
                    ViewDrafting calculations = CreateDraftingView(doc, "E-05 - Resultados preliminares e contingencia - GIS");
                    DrawGISCalculationSummary(doc, calculations, text, noteStyle, powerStyle, dataStyle);
                    sheetIds.Add(CreateSheet(doc, titleBlock, "E-05", "Cargas, N-1 e curto preliminar", calculations, text,
                        "RESULTADOS LIGADOS À MEMÓRIA\nVer entradas e limites no memorial de cálculo.\n\nATENÇÃO\nCurto simplificado não seleciona disjuntores.", s, baseName));
                }
                doc.Regenerate();
                TransactionStatus status = tx.Commit();
                if (status != TransactionStatus.Committed)
                {
                    try { doc.Close(false); } catch { }
                    throw new InvalidOperationException("Revit rolled back the model transaction after reporting a failure.");
                }
            }

            doc.SaveAs(modelPath, new SaveAsOptions { OverwriteExistingFile = true });
            Log("MODEL_SAVED " + number + " " + modelPath + " " + new FileInfo(modelPath).Length);
            if (s.Number == 19) WriteGISModelQuantities(doc, outDir);
            try
            {
                PDFExportOptions options = new PDFExportOptions { Combine = true, FileName = Path.GetFileNameWithoutExtension(pdfPath) };
                bool ok = doc.Export(Path.GetDirectoryName(pdfPath), sheetIds, options);
                Log("PDF_EXPORTED " + number + " " + ok + " " + pdfPath);
                if (s.Number == 19)
                {
                    try { ExportGIS3D(doc, axon, Path.Combine(outDir, "pranchas", "preview", "19-subestacao-gis-3d.png")); }
                    catch (Exception imageError) { Log("GIS_3D_EXPORT_FAILED " + imageError); }
                }
            }
            catch (Exception ex) { Log("PDF_FAILED " + number + " " + ex); }
            try { doc.Close(false); } catch (Exception ex) { Log("DOC_CLOSE_NOTE " + number + " " + ex.Message); }
        }

        private static List<ProjectSpec> GetSpecs()
        {
            return new List<ProjectSpec>
            {
                Spec(1,"casa-unifamiliar","Casa unifamiliar com modelagem elétrica BIM",14,10.5,2,true,"Garagem, sala integrada, cozinha, serviço, escritório, suítes e circulação familiar.","Dormitórios, suíte master, banho, closet e estar íntimo.","Iluminação residencial por ambientes, circulação e áreas de tarefa.","Tomadas gerais, cargas dedicadas de cozinha, climatização e quadro residencial.","Niveis; {4,0,4,5} {4,5,4,10.5} {9,0,9,10.5} {0,5,4,5} {9,5,14,5} {1,4,0,4,10.5} {1,9,0,9,10.5} {1,0,5,9,5}","0|GARAGEM|2|2;0|SALA / JANTAR|6.5|2;0|COZINHA|11|2;0|ESCRITORIO|2|7.5;0|SUITE|6.5|7.5;0|SERVICO|11|7.5;1|QUARTO 01|2|2;1|SUITE MASTER|6.5|2;1|BANHO / CLOSET|11|2;1|ESTAR INTIMO|2|7.5;1|QUARTO 02|6.5|7.5;1|CIRCULACAO|11|7.5",new[]{"Iluminacao por ambiente","Tomadas de uso geral","Cargas de cozinha e lavanderia","Climatizacao e chuveiros","Quadro residencial e alimentacao"},new[]{new Equipment("QDG residencial",.5,.5,.6,.4,1.8,0),new Equipment("Bancada / cooktop",9.5,.6,3.2,.8,.9,0),new Equipment("Bomba / apoio",12.2,8.5,.8,.8,1.1,0)}),
                Spec(2,"escritorio-corporativo","Escritório corporativo",20,14,2,true,"Recepção, atendimento, reunião, operação aberta, apoio e infraestrutura.","Estações de trabalho, salas de foco, colaboração, diretoria e apoio.","Zonas de luz para recepção, operação, reuniões e pavimento superior.","Tomadas, dados, salas de reunião, copa, climatização e TI.","Niveis; {5,0,5,14} {14,0,14,14} {0,5,5,5} {5,9,14,9} {14,6,20,6} {1,5,0,5,14} {1,14,0,14,14} {1,0,7,14,7}","0|RECEPCAO|2|2;0|ATENDIMENTO|10|2;0|OPERACAO ABERTA|9|7;0|SALA REUNIAO|17|3;0|COPA / APOIO|17|10;0|TI / QD|2|11;1|ESTACOES|3|4;1|FOCO|10|4;1|COLABORACAO|17|4;1|DIRETORIA|3|10;1|REUNIAO|10|10;1|APOIO|17|10",new[]{"Recepcao e operacao","Iluminacao de reunioes","Tomadas e dados de estacoes","Climatizacao e copa","TI e infraestrutura"},new[]{new Equipment("Rack telecom",1,10,.7,.6,2.0,0),new Equipment("Balcao recepcao",1,1,3.2,.8,1.1,0),new Equipment("Quadro pavimento",18,11,.5,.4,1.8,1)}),
                Spec(3,"clinica-de-saude","Clínica de saúde",24,18,1,true,"Recepcao, triagem, consultorios, exames, imagem, laboratorio, farmacia e apoio.","Consultorios e apoio clinico.","Iluminacao por recepcao, consultorios, exames e apoio clinico.","Tomadas de consultorio, imagem, laboratorio e TI; cargas especiais a confirmar.","Niveis; {6,0,6,18} {8,6,8,18} {16,6,16,18} {0,6,24,6} {0,12,8,12} {8,12,16,12} {16,12,24,12} {4,0,4,6} {12,0,12,6}","0|RECEPCAO|4|2;0|ESPERA|4|4.5;0|TRIAGEM|4|8.5;0|CONSULTORIO 01|12|3;0|CONSULTORIO 02|20|3;0|EXAMES|12|8.5;0|IMAGEM|20|8;0|LABORATORIO|4|15;0|FARMACIA|12|15;0|APOIO CLINICO|20|15;0|TI / QD|21|2",new[]{"Recepcao e espera","Consultorios","Exames e imagem","Laboratorio e farmacia","Infraestrutura e TI"},new[]{new Equipment("Tomografo",18,7,1.6,2.2,2.1,0),new Equipment("Bancada de laboratorio",2,13,4,0.8,1.1,0),new Equipment("Quadro de apoio",21,1,0.7,0.4,1.9,0)}),
                Spec(4,"escola-ensino-fundamental","Escola de ensino fundamental",30,18,2,true,"Acesso, administracao, biblioteca, refeitorio, salas de aula, laboratorios e apoio.","Salas de aula, biblioteca de apoio, informatica, ciencias e sanitarios.","Zonas por sala de aula, circulacao, biblioteca e areas comuns.","Tomadas de sala, laboratorios, audiovisual, cozinha e rede escolar.","Niveis; {0,8,30,8} {6,8,6,18} {12,8,12,18} {18,8,18,18} {24,8,24,18} {0,13,6,13} {6,13,12,13} {12,13,18,13} {18,13,24,13} {24,13,30,13} {10,0,10,8} {20,0,20,8} {1,6,0,6,18} {1,12,0,12,18} {1,18,0,18,18} {1,24,0,24,18} {1,0,6,30,6} {1,0,12,30,12}","0|RECEPCAO|5|3;0|ADMINISTRACAO|3|6;0|BIBLIOTECA|5|15;0|REFEITORIO|15|3;0|COZINHA|25|3;0|SALA MULTIUSO|25|6;0|SALA 01|3|10.5;0|SALA 02|9|10.5;0|LAB CIENCIAS|15|10.5;0|LAB INFORMATICA|21|10.5;0|SALA 03|27|10.5;1|SALA 04|3|10.5;1|SALA 05|9|10.5;1|SALA 06|15|10.5;1|SALA 07|21|10.5;1|SALA 08|27|10.5;1|BIBLIOTECA|6|4;1|APOIO PEDAGOGICO|15|4;1|SANITARIOS|26|4",new[]{"Iluminacao de salas e circulacao","Tomadas de salas e biblioteca","Laboratorios e audiovisual","Cozinha e refeitorio","TI e administracao"},new[]{new Equipment("Bancada ciencias",13,10,2.3,0.8,0.9,0),new Equipment("Rack rede escolar",2,5,0.6,0.6,1.9,1)}),
                Spec(5,"galpao-logistico","Galpão logístico",36,24,1,true,"Recebimento, armazenagem, separacao, expedicao, docas, administracao e recarga interna.","Mezanino administrativo de referencia.","Iluminacao de alto pe-direito por zonas de armazenagem, docas e expedicao.","Forca para esteiras, docas, TI, recarga interna e apoio.","Niveis; {0,8,12,8} {0,15,12,15} {12,0,12,24} {25,0,25,24} {12,12,36,12} {30,12,30,24} {25,18,36,18}","0|ADMINISTRACAO|5|4;0|RECEBIMENTO|18|5;0|ARMAZENAGEM A|18|12;0|ARMAZENAGEM B|31|8;0|SEPARACAO|19|19;0|EXPEDICAO|31|20;0|DOCAS|7|20;0|TI / QD|7|11;0|RECARGA INTERNA|31|3",new[]{"Docas e recebimento","Armazenagem de alto pe-direito","Separacao e expedicao","Administracao e TI","Recarga interna"},new[]{new Equipment("Esteira 01",14,10,7,0.8,1.0,0),new Equipment("Esteira 02",23,16,7,0.8,1.0,0),new Equipment("Mesa de triagem",15,17,3,2,1.1,0)}),
                Spec(6,"oficina-manutencao-industrial","Oficina de manutenção industrial",30,20,1,true,"Baias de manutencao, usinagem, solda, ferramentas, compressores, almoxarifado e vestiarios.","Sala de planejamento tecnico.","Zonas industriais de luminarias e tarefas por baia.","Alimentacao esquematica de maquinas, solda, compressores, bancadas e apoio.","Niveis; {0,6,30,6} {0,14,30,14} {6,0,6,6} {12,0,12,6} {18,0,18,6} {24,0,24,6} {6,6,6,14} {12,6,12,14} {18,6,18,14} {24,6,24,14} {8,14,8,20} {16,14,16,20} {23,14,23,20}","0|BAIA 01|3|10;0|BAIA 02|9|10;0|BAIA 03|15|10;0|BAIA 04|21|10;0|BAIA 05|27|10;0|USINAGEM|4|3;0|SOLDAGEM|10|3;0|FERRAMENTAS|15|3;0|COMPRESSORES|19|17;0|ALMOXARIFADO|12|17;0|VESTIARIOS|27|17;0|QD INDUSTRIAL|2|17",new[]{"Iluminacao geral e de tarefa","Maquinas por baia","Soldagem e usinagem","Ar comprimido e exaustao","Apoio e vestiarios"},new[]{new Equipment("Torno",3,8,2.4,1.2,1.6,0),new Equipment("Fresadora",9,8,2.2,1.4,1.8,0),new Equipment("Bancada de solda",15,8,2.5,1.5,1.2,0),new Equipment("Compressor",19,16,1.4,1.3,1.7,0)}),
                Spec(7,"supermercado-varejo","Supermercado e varejo",32,22,1,true,"Salão de vendas, caixas, hortifruti, padaria, frios, camaras, estoque, recebimento e administracao.","Apoio e estoque em pavimento superior de referencia.","Iluminacao por corredores, caixas, fachadas e areas de alimentos.","Cargas de refrigeracao, padaria, caixas, TI, estoque e apoio.","Niveis; {0,5,32,5} {0,16,32,16} {25,0,25,22} {25,9,32,9} {8,16,8,22} {16,16,16,22} {8,19,16,19}","0|CAIXAS|16|3;0|SALAO DE VENDAS|15|10;0|HORTIFRUTI|4|12;0|PADARIA|10|18;0|FRIOS|20|18;0|CAMARA FRIA|28|18;0|ESTOQUE|28|12;0|RECEBIMENTO|28|3;0|ADMINISTRACAO|4|3;0|QD / TI|2|20",new[]{"Corredores de vendas e caixas","Refrigeracao comercial","Padaria e preparo","Recebimento e estoque","Administracao e TI"},new[]{new Equipment("Ilha refrigerada 01",9,8,4.2,1.0,1.5,0),new Equipment("Ilha refrigerada 02",15,8,4.2,1.0,1.5,0),new Equipment("Balcao de frios",20,17,3.5,0.9,1.6,0),new Equipment("Forno de padaria",10,17,1.3,1.2,1.8,0)}),
                Spec(8,"hotel-urbano","Hotel urbano",22,16,3,true,"Recepcao, lobby, restaurante, cozinha, servicos, quartos, circulacao e apoio.","Pavimento tipo com quartos e areas de apoio.","Cenas e zonas para lobby, quartos, circulacao e servicos.","Tomadas de quartos, cozinha, lavanderia, climatizacao e TI.","Niveis; {0,7,22,7} {6,7,6,16} {16,7,16,16} {0,12,6,12} {16,12,22,12} {6,11.5,16,11.5} {11,7,11,11.5} {1,5.5,0,5.5,16} {1,11,0,11,16} {1,16.5,0,16.5,16} {1,0,7,22,7}","0|LOBBY / RECEPCAO|10|4;0|RESTAURANTE|4|11;0|COZINHA|19|11;0|SERVICOS|19|3;0|TI / QD|3|3;1|QUARTO 201|3|9.5;1|QUARTO 202|8.5|9.5;1|QUARTO 203|13.5|9.5;1|QUARTO 204|19|9.5;1|QUARTO 205|3|3.5;1|QUARTO 206|19|3.5;1|LOUNGE|11|3.5;1|CIRCULACAO|11|13.5",new[]{"Lobby e areas comuns","Quartos e circulacao","Cozinha e restaurante","Servicos e lavanderia","TI e climatizacao"},new[]{new Equipment("Balcao recepcao",7,3,3.2,0.8,1.1,0),new Equipment("Bancada cozinha",17,9,3.2,1.4,1.0,0),new Equipment("Rack telecom",2,2,0.7,0.6,2.0,0),new Equipment("Quadro pavimento tipo",20,2,0.5,0.4,1.8,1)}),
                Spec(9,"data-center","Data center",32,22,2,true,"Salas de dados, NOC, UPS, baterias, eletrica, telecomunicacoes, apoio e seguranca.","Salas de processamento redundantes e distribuicao de energia.","Iluminacao tecnica por salas e corredores.","Alimentacao esquematica de UPS, racks, climatizacao e sistemas de apoio.","Niveis; {0,7,32,7} {0,15,32,15} {7,7,7,22} {16,7,16,22} {25,7,25,22} {10,15,10,22} {10,19,25,19} {0,3,12,3} {20,0,20,7} {1,10,0,10,22} {1,21,0,21,22} {1,0,7,32,7} {1,0,15,32,15}","0|NOC|5|5;0|UPS A|4|11;0|BATERIAS|14|11;0|ELETRICA|22|11;0|GERADOR / APOIO|28|4;0|SEGURANCA|4|18;1|DATA HALL A|5|11;1|DATA HALL B|15|11;1|DATA HALL C|27|11;1|TELECOM|4|18;1|CLIMATIZACAO|16|18;1|DISTRIBUICAO|28|18",new[]{"UPS e barramentos de estudo","Salas de servidores","Climatizacao de precisao","Telecom e seguranca","Alimentacao redundante conceitual"},new[]{new Equipment("Rack 01",3,9,0.8,1.0,2.2,1),new Equipment("Rack 02",5,9,0.8,1.0,2.2,1),new Equipment("Rack 03",7,9,0.8,1.0,2.2,1),new Equipment("Rack 04",9,9,0.8,1.0,2.2,1),new Equipment("UPS modular",3,10,2.4,1.4,2.0,0),new Equipment("Banco de baterias",12,10,3.2,1.5,1.8,0)}),
                Spec(10,"estacionamento-carga-eletrica","Estacionamento com recarga de veículos elétricos",42,30,1,false,"Vagas, circulacao, acessos, nucleos de apoio, quadros e pontos de recarga.","Setores de vagas e infraestrutura de recarga.","Iluminacao de circulacao, vagas e acessos.","Carregadores por setor, quadros e sistema de monitoramento; capacidade a calcular.","Niveis; {1,1,1,29} {41,1,41,29} {1,10,41,10} {1,20,41,20} {8,10,8,20} {16,10,16,20} {24,10,24,20} {32,10,32,20} {1,5,9,5} {33,25,41,25}","0|ACESSO / CONTROLE|5|3;0|SETOR A|12|6;0|SETOR B|12|15;0|SETOR C|12|25;0|SETOR D|32|6;0|SETOR E|32|15;0|SETOR F|32|25;0|RECARGA VE|22|15;0|QUADROS|4|26",new[]{"Carregamento de veiculos por setor","Iluminacao de vagas e circulacao","Quadros e alimentadores conceituais","Controle de acesso","Supervisao e comunicacao"},new[]{new Equipment("Carregador VE 01",20,12,0.5,0.4,1.4,0),new Equipment("Carregador VE 02",22,12,0.5,0.4,1.4,0),new Equipment("Carregador VE 03",24,12,0.5,0.4,1.4,0),new Equipment("Carregador VE 04",26,12,0.5,0.4,1.4,0),new Equipment("QD carregadores",3,25,1.2,0.7,1.9,0)}),
                Spec(11,"usina-fotovoltaica","Usina fotovoltaica",60,36,1,false,"Mesas de modulos FV, inversores, transformacao, controle, acesso e cercamento de estudo.","Setores de geracao e conversao.","Iluminacao de servicos e circulacao tecnica.","Arranjos FV, inversores e transformador representados conceitualmente.","Niveis; {3,3,3,33} {57,3,57,33} {3,3,57,3} {3,33,57,33}","0|SETOR FV A|12|10;0|SETOR FV B|28|10;0|SETOR FV C|44|10;0|SETOR FV D|12|25;0|SETOR FV E|28|25;0|SETOR FV F|44|25;0|INVERSORES|54|18;0|TRANSFORMADOR|54|27;0|CONTROLE|6|5",new[]{"Mesas de modulos fotovoltaicos","Inversores por setor","Transformacao e interface de rede","Controle e monitoramento","Aterramento a desenvolver"},MakePvTables()),
                Spec(12,"subestacao-e-ccm","Subestação e centro de controle de motores",34,24,1,false,"Patio de transformacao, manobra, protecao, sala eletrica, CCM e controle.","Setores de entrada, transformacao e distribuicao.","Iluminacao de patio e salas eletricas.","Equipamentos e barramentos em arranjo funcional conceitual.","Niveis; {2,2,2,22} {32,2,32,22} {2,2,32,2} {2,22,32,22} {4,12,14,12} {20,12,32,12}","0|ENTRADA / MANOBRA|6|7;0|TRANSFORMADOR|17|7;0|PATIO DE DISTRIBUICAO|27|7;0|SALA ELETRICA|7|18;0|CCM-01|19|18;0|CONTROLE|28|18;0|QD AUXILIAR|5|4",new[]{"Entrada e protecao","Transformacao","CCM e motores","Servicos auxiliares","Controle e supervisao"},new[]{new Equipment("Transformador",15,5,4.2,3.0,3.2,0),new Equipment("Disjuntor MT",7,5,1.1,1.0,2.0,0),new Equipment("Painel CCM 01",17,15,5.5,1.0,2.2,0),new Equipment("Painel CCM 02",23,15,5.5,1.0,2.2,0)}),
                Spec(13,"estacao-tratamento-agua","Estação de tratamento de água",44,30,1,false,"Captacao, equalizacao, tratamento, filtragem, reservacao, bombeamento e controle.","Etapas de processo representadas por zonas e volumes.","Iluminacao de operacao, passarelas e casa de bombas.","Bombas, sopradores, dosagem, filtragem e supervisao.","Niveis; {2,2,2,28} {42,2,42,28} {2,2,42,2} {2,28,42,28} {4,15,40,15}","0|CAPTACAO|7|8;0|EQUALIZACAO|18|8;0|TRATAMENTO|29|8;0|FILTRAGEM|8|22;0|RESERVATORIO|21|22;0|CASA DE BOMBAS|36|22;0|CONTROLE|7|4;0|DOSAGEM|36|4",new[]{"Bombas de captacao e recalque","Processo e filtragem","Dosagem e sopradores","Reservacao","Controle e instrumentacao"},new[]{new Equipment("Tanque equalizacao",13,4,7,7,3.5,0),new Equipment("Tanque processo",24,4,8,7,3.5,0),new Equipment("Reservatorio",17,18,10,7,3.2,0),new Equipment("Bomba 01",35,19,1.4,1.4,1.5,0),new Equipment("Bomba 02",38,19,1.4,1.4,1.5,0)}),
                Spec(14,"microrede-fv-bess-gerador","Microrrede com geração solar, baterias e gerador de apoio",50,32,1,false,"Geracao solar, armazenamento BESS, gerador de apoio, transformacao, cargas e controle.","Zonas de fonte, conversao, armazenamento e cargas.","Iluminacao de servicos, circulacao e areas de equipamento.","Diagrama conceitual de fontes, conversores, BESS, gerador e cargas.","Niveis; {2,2,2,30} {48,2,48,30} {2,2,48,2} {2,30,48,30} {20,4,20,28} {35,4,35,28}","0|CAMPO FV|10|22;0|INVERSORES|25|22;0|BESS|40|22;0|GERADOR|9|8;0|TRANSFORMADOR|24|8;0|QGBT / CARGAS|40|8;0|CONTROLE|25|15",new[]{"Geracao fotovoltaica","Conversao e transformacao","Armazenamento BESS","Gerador de apoio","Supervisao e cargas"},MakeMicrogridEquipment())
                ,Spec(19,"subestacao-gis","Subestação de alta tensão GIS",64,42,1,false,"Subestacao 138/13,8 kV com cinco bays GIS, dois transformadores de potencia, barras seccionadas, cubiculos MT, protecao, controle e servicos auxiliares.","Sala GIS, sala de cubiculos, casa de controle, patio de transformacao e canaletas.","Pontos de iluminacao do patio e ambientes tecnicos; apenas indicativos.","Distribuicao de potencia 138/13,8 kV e fontes auxiliares; sem circuitos MEP conectados.","Niveis;","0|GIS 138 kV|20|16;0|Cubiculos 13,8 kV|39|16;0|Controle / protecao|14|34;0|Transformador TR-01|29|31;0|Transformador TR-02|45|31;0|Servicos auxiliares|54|16;0|Canaletas|36|26",new[]{"Linha 138 kV e bays GIS","Barras 138 kV A/B e acoplamento","Transformadores 25 MVA 138/13,8 kV","Cubiculos 13,8 kV A/B","Protecao, controle e telecomunicacoes","Servicos auxiliares CA/CC","Canaletas e malha conceitual"},Array.Empty<Equipment>())
            };
        }

        private static ProjectSpec Spec(int n,string slug,string title,double w,double d,int floors,bool enclosed,string program,string upper,string light,string power,string segments,string spaces,string[] systems,Equipment[] equipment)
        {
            var s = new ProjectSpec { Number=n,Slug=slug,Title=title,Width=w,Depth=d,Floors=floors,Enclosed=enclosed,Program=program,UpperProgram=upper,LightNote=light,PowerNote=power,Systems=systems,Equipment=equipment };
            foreach(string raw in segments.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))
            {
                if(!raw.StartsWith("{")) continue;
                string[] v=raw.Trim('{','}').Split(',');
                if(v.Length==4)s.Partitions.Add(new Segment(0,double.Parse(v[0],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[3],System.Globalization.CultureInfo.InvariantCulture)));
                else if(v.Length==5)s.Partitions.Add(new Segment(int.Parse(v[0]),double.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[3],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[4],System.Globalization.CultureInfo.InvariantCulture)));
            }
            foreach(string raw in spaces.Split(';'))
            {
                string[] v=raw.Split('|'); if(v.Length==4)s.Spaces.Add(new Space(int.Parse(v[0]),v[1],double.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture),double.Parse(v[3],System.Globalization.CultureInfo.InvariantCulture)));
            }
            return s;
        }

        private static Equipment[] MakePvTables()
        {
            var a=new List<Equipment>(); int id=0;
            foreach(double y in new[]{7.0,12.0,17.0,22.0,27.0}) foreach(double x in new[]{8.0,20.0,32.0,44.0}) a.Add(new Equipment("Mesa FV " + (++id),x,y,8.5,2.3,0.28,0));
            a.Add(new Equipment("Inversor A",52,16,1.2,0.8,1.6,0));a.Add(new Equipment("Inversor B",54,18,1.2,0.8,1.6,0));a.Add(new Equipment("Transformador",53,26,3.5,2.5,2.6,0));return a.ToArray();
        }

        private static Equipment[] MakeMicrogridEquipment()
        {
            var a=new List<Equipment>();int id=0;
            foreach(double y in new[]{23.0,26.0,29.0})foreach(double x in new[]{5.0,14.0,23.0,32.0})a.Add(new Equipment("Mesa FV " + (++id),x,y,6.5,1.5,0.25,0));
            a.Add(new Equipment("BESS rack A",36,20,3.4,2.5,2.4,0));a.Add(new Equipment("BESS rack B",40,20,3.4,2.5,2.4,0));a.Add(new Equipment("Gerador",6,6,4.0,2.2,2.2,0));a.Add(new Equipment("Transformador",24,6,3.0,2.6,2.8,0));a.Add(new Equipment("Inversor",26,20,1.2,0.8,1.7,0));return a.ToArray();
        }

        private static void BuildShell(Document doc,WallType type,Level level,ProjectSpec spec,bool ground)
        {
            double w=spec.Width,d=spec.Depth,h=M(WallHeight(spec)); Wall south=AddWall(doc,type,level,0,0,w,0,h); Wall west=AddWall(doc,type,level,0,0,0,d,h); Wall east=AddWall(doc,type,level,w,0,w,d,h); Wall north=AddWall(doc,type,level,0,d,w,d,h);
            double baseElevation=UnitUtils.ConvertFromInternalUnits(level.Elevation,UnitTypeId.Meters);
            if(ground)
            {
                Open(doc,south,w*.46,0,w*.54,0,0,2.25);Open(doc,south,w*.10,0,w*.22,0,.9,2.35);Open(doc,south,w*.76,0,w*.89,0,.9,2.35);
                AddBoxSolid(doc,"Porta principal",w*.46,-.035,w*.08,.07,baseElevation,2.18,null);
                AddBoxSolid(doc,"Esquadria frontal 01",w*.10,-.035,w*.12,.07,baseElevation+.9,1.45,null);
                AddBoxSolid(doc,"Esquadria frontal 02",w*.76,-.035,w*.13,.07,baseElevation+.9,1.45,null);
            }
            else
            {
                Open(doc,south,w*.12,0,w*.28,0,.9,2.35);Open(doc,south,w*.70,0,w*.88,0,.9,2.35);
                AddBoxSolid(doc,"Esquadria superior 01",w*.12,-.035,w*.16,.07,baseElevation+.9,1.45,null);
                AddBoxSolid(doc,"Esquadria superior 02",w*.70,-.035,w*.18,.07,baseElevation+.9,1.45,null);
            }
            Open(doc,west,0,d*.18,0,d*.31,.9,2.35);Open(doc,east,w,d*.62,w,d*.78,.9,2.35);Open(doc,north,w*.15,d,w*.27,d,.9,2.35);Open(doc,north,w*.68,d,w*.83,d,.9,2.35);
            AddBoxSolid(doc,"Esquadria lateral oeste",-.035,d*.18,.07,d*.13,baseElevation+.9,1.45,null);
            AddBoxSolid(doc,"Esquadria lateral leste",w-.035,d*.62,.07,d*.16,baseElevation+.9,1.45,null);
            AddBoxSolid(doc,"Esquadria posterior 01",w*.15,d-.035,w*.12,.07,baseElevation+.9,1.45,null);
            AddBoxSolid(doc,"Esquadria posterior 02",w*.68,d-.035,w*.15,.07,baseElevation+.9,1.45,null);
        }

        private static void DrawSpaces(Document doc,View view,ElementId text,IEnumerable<Space> spaces,double z)
        {foreach(Space p in spaces)TextNote.Create(doc,view.Id,XY(p.X,p.Y,z),p.Name,text);}

        private static void DrawEquipmentPlan(Document doc,View view,ElementId text,GraphicsStyle style,ProjectSpec spec,double z)
        {
            foreach (Tuple<Equipment, string> item in EquipmentPlanTags(spec))
            {
                Equipment e = item.Item1;
                string label = item.Item2;
                if ((spec.Number == 11 || spec.Number == 14) &&
                    label.StartsWith("FV-", StringComparison.Ordinal))
                    label = "";
                Box(doc,view,e.X,e.Y,e.X+e.W,e.Y+e.D,label,text,style,z);
            }
        }

        private static bool IsEquipmentLabelZone(Space space,List<Equipment> equipment)
        {
            const double margin = 0.65;
            return equipment.Any(e => space.X >= e.X - margin && space.X <= e.X + e.W + margin &&
                                      space.Y >= e.Y - margin && space.Y <= e.Y + e.D + margin);
        }

        private static List<Tuple<Equipment,string>> EquipmentPlanTags(ProjectSpec spec)
        {
            var result = new List<Tuple<Equipment,string>>();
            int generalIndex = 0;
            foreach (Equipment equipment in spec.Equipment.Where(x => x.Floor == 0))
            {
                string name = equipment.Name.Trim();
                string tag;
                if (name.StartsWith("Mesa FV ", StringComparison.OrdinalIgnoreCase))
                    tag = "FV-" + name.Substring("Mesa FV ".Length).Trim();
                else if (name.StartsWith("Carregador VE ", StringComparison.OrdinalIgnoreCase))
                    tag = "C" + name.Substring("Carregador VE ".Length).Trim().TrimStart('0');
                else if (name.StartsWith("BESS rack ", StringComparison.OrdinalIgnoreCase))
                    tag = "BAT-" + name.Substring("BESS rack ".Length).Trim();
                else
                {
                    generalIndex++;
                    tag = "E" + generalIndex.ToString("00");
                }
                result.Add(Tuple.Create(equipment, tag));
            }
            return result;
        }

        private static string BuildEquipmentLegend(ProjectSpec spec)
        {
            List<Tuple<Equipment,string>> items = EquipmentPlanTags(spec);
            var lines = new List<string>();
            AddEquipmentSeries(items, "FV-", "Mesas fotovoltaicas", lines);
            AddEquipmentSeries(items, "C", "Carregadores de veículos elétricos", lines);
            AddEquipmentSeries(items, "BAT-", "Racks BESS", lines);
            foreach (Tuple<Equipment,string> item in items.Where(x =>
                         !x.Item2.StartsWith("FV-", StringComparison.Ordinal) &&
                         !x.Item2.StartsWith("BAT-", StringComparison.Ordinal) &&
                         !x.Item1.Name.StartsWith("Carregador VE ", StringComparison.OrdinalIgnoreCase)))
                lines.Add(item.Item2 + ": " + item.Item1.Name);
            return lines.Count == 0 ? "Sem equipamentos listados." : string.Join("\n", lines);
        }

        private static void AddEquipmentSeries(List<Tuple<Equipment,string>> items,string tagPrefix,string label,List<string> lines)
        {
            List<Tuple<Equipment,string>> series = items.Where(x => x.Item2.StartsWith(tagPrefix, StringComparison.Ordinal)).ToList();
            if (series.Count == 0) return;
            if (series.Count == 1) lines.Add(series[0].Item2 + ": " + series[0].Item1.Name);
            else lines.Add(series[0].Item2 + " a " + series[series.Count - 1].Item2 + ": " + label + " (" + series.Count + " unidades)");
        }

        private static void DrawFurniturePlan(Document doc,View view,ElementId text,ProjectSpec s,int floor)
        {
            GraphicsStyle style=GetLineStyle(doc,"BIM-MOBILIARIO",new Color(135,142,151),1);
            Action<double,double,double,double> rect=(x,y,w,d)=>Box(doc,view,x,y,x+w,y+d,"",text,style,0);
            switch(s.Number)
            {
                case 1:
                    if(floor==0){rect(5.1,.7,1.4,.85);rect(5.5,5.2,2.3,.85);rect(9.8,.55,2.6,.48);rect(1.0,6.8,1.9,.85);}
                    else{rect(1.0,1.05,2.1,1.45);rect(5.35,1.05,2.2,1.45);rect(1.0,6.55,2.1,1.45);rect(5.35,6.55,2.2,1.45);}
                    break;
                case 2:
                    for(int row=0;row<2;row++)for(int col=0;col<4;col++)rect(6.0+col*1.7,5.2+row*1.4,.9,.55);
                    if(floor==1){rect(2.0,2.0,3.2,1.0);rect(8.2,2.0,3.2,1.0);rect(14.0,2.0,3.2,1.0);}
                    break;
                case 3:
                    for(int i=0;i<4;i++)rect(9.2+(i%2)*8,2.0+(i/2)*7.5,2.1,.85);
                    rect(1.3,13.3,4.0,.65);rect(9.5,13.3,4.0,.65);rect(18.0,13.3,4.0,.65);
                    break;
                case 4:
                    for(int room=0;room<5;room++)for(int row=0;row<2;row++)for(int col=0;col<2;col++)rect(1.1+room*6+col*1.6,9.1+row*1.6,.75,.48);
                    if(floor==0){rect(2.0,14.2,4.8,.55);rect(8.0,14.2,4.8,.55);}
                    break;
                case 5:
                    for(int i=0;i<5;i++)rect(14.0+i*3.8,9.1,.7,8.0);
                    rect(13.5,18.2,9.5,.7);rect(23.5,18.2,8.5,.7);
                    break;
                case 6:
                    for(int i=0;i<5;i++){rect(1.0+i*5.8,8.0,2.0,2.6);rect(1.2+i*5.8,2.0,2.6,1.1);}
                    break;
                case 7:
                    for(int row=0;row<4;row++)rect(8.0,7.0+row*2.1,15.0,.7);
                    for(int i=0;i<4;i++)rect(13.0+i*1.8,1.4,.7,2.1);
                    break;
                case 8:
                    if(floor>0){double[][] beds={new double[]{1.1,8.0},new double[]{6.5,8.0},new double[]{12.0,8.0},new double[]{17.2,8.0},new double[]{1.1,2.0},new double[]{17.2,2.0}};foreach(double[] p in beds)rect(p[0],p[1],2.0,2.9);}
                    else{rect(2.0,2.0,5.0,.8);rect(12.0,10.0,5.2,.8);rect(8.0,4.1,4.0,1.4);}
                    break;
                case 9:
                    for(int row=0;row<3;row++)for(int col=0;col<8;col++)rect(2.0+col*3.6,8.0+row*2.0,.65,1.1);
                    break;
                case 10:
                    for(int row=0;row<3;row++){double y=1.8+row*9.4;for(int col=0;col<12;col++)DrawLine(doc,view,2.7+col*3.15,y,2.7+col*3.15,y+5.6,style,0);}
                    break;
                case 11:
                    for(int row=0;row<5;row++)for(int col=0;col<4;col++)rect(7.8+col*12.0,6.8+row*5.0,8.5,2.3);
                    break;
                case 12:
                    rect(4.5,4.2,8.2,6.2);rect(17.0,4.2,12.0,6.2);for(int i=0;i<5;i++)DrawLine(doc,view,5+i*5.6,12.5,5+i*5.6,20.5,style,0);
                    break;
                case 13:
                    Circle(doc,view,13.5,7.5,3.2,style,0);Circle(doc,view,27.5,7.5,3.4,style,0);Circle(doc,view,22.0,21.0,4.4,style,0);rect(34.8,19.0,5.4,4.0);
                    break;
                case 14:
                    for(int row=0;row<3;row++)for(int col=0;col<4;col++)rect(4.8+col*9.0,22.1+row*2.7,6.5,1.4);
                    rect(35.5,19.8,3.5,2.8);rect(39.5,19.8,3.5,2.8);rect(5.0,5.5,4.5,2.3);
                    break;
            }
        }

        private static void DrawSystemPlan(Document doc,View view,ElementId text,ProjectSpec spec,GraphicsStyle style,bool lighting,double z)
        {
            var rooms=spec.Spaces.Where(x=>x.Floor==0).ToList();
            foreach(Space p in rooms)TextNote.Create(doc,view.Id,XY(p.X,p.Y,z),p.Name,text);
            double panelX=Math.Max(.4,spec.Width*.08),panelY=Math.Max(.4,spec.Depth*.08);
            Box(doc,view,panelX-.22,panelY-.16,panelX+.22,panelY+.16,lighting?"QD-L":"QD-01",text,GetLineStyle(view.Document,"BIM-ANOTACOES",new Color(53,59,66),2),z);
            int index=0;
            foreach(Space p in rooms)
            {
                index++; double x=p.X+(lighting?1.35:1.55),y=p.Y+(lighting?1.05:.75);
                string tag=(lighting?"L":"T")+index.ToString("00");
                Circle(doc,view,x,y,lighting?.15:.11,style,z);TextNote.Create(doc,view.Id,XY(x+.18,y+.12,z),tag,text);
                if(!lighting && index%2==0)
                {
                    double dx=p.X+.48,dy=p.Y+1.55;
                    Box(doc,view,dx-.09,dy-.09,dx+.09,dy+.09,"",text,GetLineStyle(view.Document,"E-DADOS",new Color(38,133,102),4),z);TextNote.Create(doc,view.Id,XY(dx+.12,dy+.1,z),"D"+index.ToString("00"),text);
                }
            }
            TextNote.Create(doc,view.Id,XY(.2,-.55,z),lighting?"PONTOS DE LUZ POR AMBIENTE - POSICOES ESQUEMATICAS":"PONTOS DE FORCA / DADOS - POSICOES ESQUEMATICAS",text);
        }

        private static void DrawGISEquipmentPlan(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power)
        {
            TextNote.Create(doc,view.Id,XY(.7,10.0,0),"ARRANJO ESQUEMATICO | ZONAS E EQUIPAMENTOS GIS",text);
            Box(doc,view,.5,.6,18.1,9.6,"",text,note,0);
            Box(doc,view,.9,4.3,10.0,9.1,"",text,note,0);
            TextNote.Create(doc,view.Id,XY(1.2,8.85,0),"EDIFICIO GIS 138 kV",text);
            string[] bays={"LB-01","TR-01","BC-01","TR-02","LB-02"};
            for(int i=0;i<bays.Length;i++)
                Box(doc,view,1.2+i*1.68,7.4,2.65+i*1.68,8.4,bays[i],text,power,0);
            Box(doc,view,1.2,6.3,9.6,6.9,"BARRAS 138 kV A / B",text,note,0);
            Box(doc,view,1.2,4.8,9.6,5.8,"CUBICULOS 13,8 kV A/B",text,power,0);
            Box(doc,view,.9,1.2,4.9,3.5,"PROTECAO / CONTROLE\nIEDs, HMI e gateway",text,note,0);
            Box(doc,view,5.4,1.2,9.6,3.5,"SERVICOS AUXILIARES\nCA, CC e UPS",text,note,0);
            Box(doc,view,10.7,6.5,14.1,8.9,"TR-01\n25 MVA",text,power,0);
            Box(doc,view,14.7,6.5,18.0,8.9,"TR-02\n25 MVA",text,power,0);
            Box(doc,view,11.4,3.5,17.7,5.3,"PATIO / EQUIPAMENTOS AUXILIARES",text,note,0);
            DrawLine(doc,view,4.9,.95,11.4,.95,note,0);
            DrawLine(doc,view,11.4,.95,11.4,3.5,note,0);
            TextNote.Create(doc,view.Id,XY(5.2,.68,0),"CANALETAS DE POTENCIA E CONTROLE",text);
        }

        private static void DrawGISSystemPlan(Document doc,ViewDrafting view,ElementId text,GraphicsStyle system,GraphicsStyle note,bool lighting)
        {
            DrawGISEquipmentPlan(doc,view,text,note,system);
            if(lighting)
            {
                AddGISTag(doc,view,7.5,8.8,"L01",text,system);
                AddGISTag(doc,view,8.7,4.2,"L02",text,system);
                AddGISTag(doc,view,4.2,3.75,"L03",text,system);
                AddGISTag(doc,view,11.2,9.1,"L04",text,system);
                AddGISTag(doc,view,15.5,9.1,"L05",text,system);
                AddGISTag(doc,view,16.0,5.65,"L06",text,system);
                AddGISTag(doc,view,10.6,1.05,"L07",text,system);
            }
            else
            {
                AddGISTag(doc,view,7.5,8.8,"T01",text,system);
                AddGISTag(doc,view,8.7,4.2,"T02",text,system);
                AddGISTag(doc,view,4.2,3.75,"T03",text,system);
                AddGISTag(doc,view,11.2,6.1,"T04",text,system);
                AddGISTag(doc,view,13.4,6.1,"D04",text,note);
                AddGISTag(doc,view,15.5,6.1,"T05",text,system);
                AddGISTag(doc,view,16.0,5.65,"T06 / D06",text,system);
                AddGISTag(doc,view,10.6,1.05,"T07",text,system);
            }
        }

        private static void AddGISTag(Document doc,ViewDrafting view,double x,double y,string label,ElementId text,GraphicsStyle style)
        {
            Circle(doc,view,x,y,.12,style,0);
            TextNote.Create(doc,view.Id,XY(x+.18,y+.08,0),label,text);
        }

        private static string BuildGISEquipmentLegend()
        {
            return "GIS-LB-01 / LB-02: bays de linha e reserva\nGIS-TR-01 / TR-02: bays de transformador\nGIS-BC-01: acoplamento das barras\nTR-01 / TR-02: transformadores 25 MVA\nMV-CUB-A01 a A07: cubiculos 13,8 kV\nIED-LINE / BUS / TR: protecao\nAUX-CA / AUX-CC / UPS: servicos auxiliares";
        }

        private static void DrawDistribution(Document doc,ViewDrafting view,ElementId text,ProjectSpec s,GraphicsStyle note,GraphicsStyle light,GraphicsStyle power,GraphicsStyle data)
        {
            if (s.Number == 19)
            {
                DrawGISSingleLine(doc, view, text, note, power);
                return;
            }
            TextNote.Create(doc,view.Id,XY(.2,7.5,0),"DIAGRAMA FUNCIONAL - "+s.Title.ToUpperInvariant()+" - ESTUDO",text);
            switch(s.Number)
            {
                case 1: DrawHouseDiagram(doc,view,text,note,light,power);break;
                case 2: DrawOfficeDiagram(doc,view,text,note,power,data);break;
                case 3: DrawClinicDiagram(doc,view,text,note,power,data);break;
                case 4: DrawSchoolDiagram(doc,view,text,note,light,power);break;
                case 5: DrawWarehouseDiagram(doc,view,text,note,power);break;
                case 6: DrawWorkshopDiagram(doc,view,text,note,power);break;
                case 7: DrawRetailDiagram(doc,view,text,note,power);break;
                case 8: DrawHotelDiagram(doc,view,text,note,light,power);break;
                case 9: DrawDataCenterDiagram(doc,view,text,note,power,data);break;
                case 10: DrawEvDiagram(doc,view,text,note,power,data);break;
                case 11: DrawPvDiagram(doc,view,text,note,power,light);break;
                case 12: DrawSubstationDiagram(doc,view,text,note,power);break;
                case 13: DrawWaterDiagram(doc,view,text,note,power,data);break;
                case 14: DrawMicrogridDiagram(doc,view,text,note,power,light,data);break;
                default: DrawGenericSystemDiagram(doc,view,text,s,note,light,power,data);break;
            }
            TextNote.Create(doc,view.Id,XY(.25,-.45,0),"ESTUDO CONCEITUAL | Sem dimensionamento de demanda, cabos, protecoes, seletividade ou aterramento.",text);
        }

        private static void DrawHouseDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle l,GraphicsStyle p)
        {Box(d,v,3.0,6.1,5.7,6.8,"MEDICAO / PROTECAO",t,n,0);Box(d,v,3.0,4.6,5.7,5.3,"QD-01 RESIDENCIAL",t,n,0);DrawLine(d,v,4.35,6.1,4.35,5.3,n,0);DrawLine(d,v,4.35,4.6,4.35,3.9,n,0);DrawLine(d,v,1.2,3.9,7.5,3.9,n,0);string[] a={"ILUMINACAO","TUG / DORMITORIOS","COZINHA / LAVANDERIA","CHUVEIROS / CLIMA"};for(int i=0;i<a.Length;i++){double x=.3+i*2.0;DrawLine(d,v,x+.7,3.9,x+.7,3.2,n,0);Box(d,v,x,2.2,x+1.4,3.2,a[i],t,i==0?l:p,0);}}

        private static void DrawOfficeDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle data)
        {Box(d,v,.7,6.0,3.1,6.7,"ENTRADA / MEDICAO",t,n,0);Box(d,v,4.0,6.0,6.7,6.7,"QGBT-01",t,n,0);DrawLine(d,v,3.1,6.35,4.0,6.35,n,0);DrawLine(d,v,5.35,6.0,5.35,1.6,n,0);for(int i=0;i<2;i++){double y=4.9-i*2.1;DrawLine(d,v,5.35,y,6.8,y,n,0);Box(d,v,6.8,y-.45,9.2,y+.45,"QD PAVIMENTO "+(i+1),t,p,0);DrawLine(d,v,9.2,y,10.4,y,data,0);Box(d,v,10.4,y-.45,12.8,y+.45,i==0?"OPERACAO / SALAS":"ESTACOES / FOCO",t,data,0);}}

        private static void DrawClinicDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle data)
        {Box(d,v,.5,5.8,2.3,6.4,"MEDICAO / ENTRADA",t,n,0);Box(d,v,3,5.8,5.4,6.4,"QGBT-01",t,n,0);DrawLine(d,v,2.3,6.1,3,6.1,n,0);DrawLine(d,v,4.2,5.8,4.2,4.7,n,0);Box(d,v,1.0,3.7,3.7,4.7,"QD-ESSENCIAL\nUPS / EXAMES",t,p,0);Box(d,v,4.7,3.7,7.4,4.7,"QD-CLINICO\nCONSULTORIOS",t,p,0);DrawLine(d,v,2.35,3.7,2.35,2.8,data,0);DrawLine(d,v,6.05,3.7,6.05,2.8,data,0);Box(d,v,.5,1.8,4.2,2.8,"IMAGEM / LABORATORIO",t,data,0);Box(d,v,4.7,1.8,8.4,2.8,"RECEPCAO / APOIO",t,data,0);}

        private static void DrawSchoolDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle l,GraphicsStyle p)
        {Box(d,v,3.1,5.8,5.8,6.5,"ENTRADA",t,n,0);Box(d,v,3.1,4.5,5.8,5.2,"QGBT ESCOLAR",t,n,0);DrawLine(d,v,4.45,5.8,4.45,5.2,n,0);DrawLine(d,v,4.45,4.5,4.45,3.7,n,0);DrawLine(d,v,1,3.7,8,3.7,n,0);foreach(double x in new[]{.5,2.5,4.5,6.5}){DrawLine(d,v,x+0.5,3.7,x+0.5,3.1,n,0);Box(d,v,x,2.1,x+1.0,3.1,(x<4?"QD T": "QD S")+((int)(x*10)).ToString(),t,p,0);}TextNote.Create(d,v.Id,XY(.8,1.2,0),"SALAS | LABORATORIOS | BIBLIOTECA | COZINHA",t);}

        private static void DrawWarehouseDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p)
        {Box(d,v,.5,5.8,2.5,6.5,"MEDICAO",t,n,0);Box(d,v,3.2,5.8,5.4,6.5,"QGBT LOGISTICA",t,n,0);DrawLine(d,v,2.5,6.15,3.2,6.15,n,0);DrawLine(d,v,4.3,5.8,4.3,4.8,n,0);Box(d,v,1,3.8,3.6,4.8,"MCC ESTEIRAS",t,p,0);Box(d,v,5,3.8,7.6,4.8,"QD DOCAS",t,p,0);Box(d,v,1,1.8,3.6,2.8,"MOTORES / TRIAGEM",t,p,0);Box(d,v,5,1.8,7.6,2.8,"PORTAS / RECARGA",t,p,0);DrawLine(d,v,2.3,3.8,2.3,2.8,p,0);DrawLine(d,v,6.3,3.8,6.3,2.8,p,0);DrawLine(d,v,4.3,4.8,4.3,4.2,n,0);DrawLine(d,v,2.3,4.2,6.3,4.2,n,0);DrawLine(d,v,2.3,4.2,2.3,3.8,n,0);DrawLine(d,v,6.3,4.2,6.3,3.8,n,0);}

        private static void DrawWorkshopDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p)
        {Box(d,v,.6,5.8,2.8,6.5,"ALIMENTACAO",t,n,0);Box(d,v,3.4,5.8,5.9,6.5,"QGBT / MCC-01",t,n,0);DrawLine(d,v,2.8,6.15,3.4,6.15,n,0);DrawLine(d,v,4.65,5.8,4.65,4.6,n,0);foreach(int i in Enumerable.Range(0,4)){double x=.4+i*2.0;DrawLine(d,v,4.65,4.6,x+0.8,4.6,p,0);DrawLine(d,v,x+0.8,4.6,x+0.8,3.9,p,0);Box(d,v,x,2.8,x+1.6,3.9,new[]{"USINAGEM","SOLDAGEM","BAIAS","COMPRESSOR"}[i],t,p,0);}TextNote.Create(d,v.Id,XY(.6,1.8,0),"ALIMENTADORES DE MAQUINAS E BANCADAS - ESTUDO",t);}

        private static void DrawRetailDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p)
        {Box(d,v,.5,5.8,2.5,6.5,"MEDICAO",t,n,0);Box(d,v,3.2,5.8,5.8,6.5,"QGBT VAREJO",t,n,0);DrawLine(d,v,2.5,6.15,3.2,6.15,n,0);DrawLine(d,v,4.5,5.8,4.5,4.8,n,0);Box(d,v,.5,3.8,3.6,4.8,"QDF LOJA",t,p,0);Box(d,v,5.4,3.8,8.5,4.8,"QDF FRIO",t,p,0);DrawLine(d,v,2,3.8,2,2.9,p,0);DrawLine(d,v,6.9,3.8,6.9,2.9,p,0);Box(d,v,.5,1.8,3.6,2.9,"CAIXAS / ILHAS",t,p,0);Box(d,v,5.4,1.8,8.5,2.9,"CAMARAS / FRIOS",t,p,0);DrawLine(d,v,4.5,4.8,4.5,4.25,n,0);DrawLine(d,v,2,4.25,6.9,4.25,n,0);DrawLine(d,v,2,4.25,2,3.8,n,0);DrawLine(d,v,6.9,4.25,6.9,3.8,n,0);}

        private static void DrawHotelDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle l,GraphicsStyle p)
        {Box(d,v,.6,5.9,2.8,6.6,"ENTRADA / QGBT",t,n,0);DrawLine(d,v,1.7,5.9,1.7,1.7,n,0);for(int f=0;f<3;f++){double y=4.9-f*1.5;DrawLine(d,v,1.7,y,3.0,y,n,0);Box(d,v,3.0,y-.45,5.5,y+.45,"QD PAVIMENTO "+(f+1),t,p,0);DrawLine(d,v,5.5,y,6.7,y,l,0);Box(d,v,6.7,y-.45,8.8,y+.45,f==0?"LOBBY / COZINHA":"QUARTOS / APOIO",t,l,0);}}

        private static void DrawDataCenterDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle data)
        {Box(d,v,3.0,6.1,5.6,6.8,"ENTRADA MT",t,n,0);Box(d,v,.5,4.5,3.5,5.3,"TRANSFORMADOR A",t,p,0);Box(d,v,5.1,4.5,8.1,5.3,"TRANSFORMADOR B",t,p,0);DrawLine(d,v,4.3,6.1,2,5.3,n,0);DrawLine(d,v,4.3,6.1,6.6,5.3,n,0);Box(d,v,.5,2.8,3.5,3.6,"UPS A / BARRA A",t,p,0);Box(d,v,5.1,2.8,8.1,3.6,"UPS B / BARRA B",t,p,0);DrawLine(d,v,2,4.5,2,3.6,p,0);DrawLine(d,v,6.6,4.5,6.6,3.6,p,0);DrawLine(d,v,2,2.8,2,2.0,data,0);DrawLine(d,v,6.6,2.8,6.6,2.0,data,0);Box(d,v,.5,1.1,3.5,2.0,"RACKS A",t,data,0);Box(d,v,5.1,1.1,8.1,2.0,"RACKS B",t,data,0);}

        private static void DrawEvDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle data)
        {Box(d,v,3.0,6.0,5.6,6.7,"REDE / MEDICAO",t,n,0);Box(d,v,3.0,4.4,5.6,5.1,"QGBT ESTACIONAMENTO",t,p,0);DrawLine(d,v,4.3,6.0,4.3,5.1,n,0);Box(d,v,3.0,2.8,5.6,3.5,"CONTROLE DE DEMANDA",t,data,0);DrawLine(d,v,4.3,4.4,4.3,3.5,p,0);foreach(int i in Enumerable.Range(0,4)){double x=.5+i*2.0;DrawLine(d,v,4.3,2.8,x+.7,2.0,data,0);Box(d,v,x,.9,x+1.4,2.0,"CARREGADOR "+(i+1),t,p,0);}}

        private static void DrawPvDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle l)
        {string[] a={"MESAS FV / STRINGS","CAIXA COMBINADORA","INVERSOR CC/CA","TRANSFORMADOR","PONTO DE CONEXAO"};double[] x={.3,2.2,4.1,6.0,7.9};for(int i=0;i<a.Length;i++){Box(d,v,x[i],3.2,x[i]+1.6,4.3,a[i],t,i<2?l:p,0);if(i>0)DrawLine(d,v,x[i-1]+1.6,3.75,x[i],3.75,n,0);}TextNote.Create(d,v.Id,XY(.5,2.1,0),"FLUXO DE GERACAO SOLAR EM CORRENTE CONTINUA E ALTERNADA",t);}

        private static void DrawSubstationDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p)
        {string[] a={"ENTRADA MT","PROTECAO / MANOBRA","TRANSFORMADOR","QGBT / BARRAMENTO","CCM / CARGAS"};double y=5.9;for(int i=0;i<a.Length;i++){Box(d,v,.6,y-i*1.0,3.2,y+.65-i*1.0,a[i],t,i<3?n:p,0);if(i<4)DrawLine(d,v,1.9,y-i*1.0,1.9,y-.35-i*1.0,n,0);}TextNote.Create(d,v.Id,XY(4.2,4.4,0),"PATIO ABERTO\nDISJUNCAO E TRANSFORMACAO\nSALA DE CONTROLE / CCM",t);}

        private static void DrawWaterDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle data)
        {string[] a={"CAPTACAO","EQUALIZACAO","BOMBEAMENTO","FILTRAGEM","RESERVACAO"};double[] x={.2,2.0,3.8,5.6,7.4};for(int i=0;i<a.Length;i++){Box(d,v,x[i],3.4,x[i]+1.5,4.5,a[i],t,i==2?p:n,0);if(i>0)DrawLine(d,v,x[i-1]+1.5,3.95,x[i],3.95,n,0);}Box(d,v,3.5,1.2,5.5,2.1,"AUTOMACAO / CLP",t,data,0);DrawLine(d,v,4.5,2.1,4.5,3.4,data,0);}

        private static void DrawMicrogridDiagram(Document d,View v,ElementId t,GraphicsStyle n,GraphicsStyle p,GraphicsStyle l,GraphicsStyle data)
        {Box(d,v,.3,5.6,2.4,6.5,"FV / INVERSOR",t,l,0);Box(d,v,.3,3.6,2.4,4.5,"BESS / PCS",t,p,0);Box(d,v,.3,1.6,2.4,2.5,"GERADOR",t,n,0);DrawLine(d,v,2.4,6.05,4.0,4.8,n,0);DrawLine(d,v,2.4,4.05,4.0,4.3,n,0);DrawLine(d,v,2.4,2.05,4.0,3.8,n,0);Box(d,v,4.0,3.5,6.0,4.8,"BARRAMENTO CA",t,n,0);DrawLine(d,v,6.0,4.15,7.3,4.15,data,0);Box(d,v,7.3,3.5,9.3,4.8,"CARGAS PRIORITARIAS",t,data,0);}

        private static void DrawGenericSystemDiagram(Document doc,ViewDrafting view,ElementId text,ProjectSpec s,GraphicsStyle note,GraphicsStyle light,GraphicsStyle power,GraphicsStyle data)
        {Box(doc,view,.5,5.4,2.5,6.2,"MEDICAO / ENTRADA",text,note,0);Box(doc,view,3.4,5.4,5.8,6.2,"QGBT-01",text,note,0);DrawLine(doc,view,2.5,5.8,3.4,5.8,note,0);DrawLine(doc,view,4.6,5.4,4.6,4.7,note,0);for(int i=0;i<Math.Min(5,s.Systems.Length);i++){double x=.3+i*1.85;DrawLine(doc,view,4.6,4.7,x+.75,3.8,note,0);Box(doc,view,x,2.7,x+1.5,3.8,s.Systems[i],text,new[]{light,power,data,power,light}[i],0);}}

        private static ElementId CreateSheet(Document doc,ElementId block,string number,string name,View view,ElementId text,string notes,ProjectSpec spec,string baseName)
        {
            ViewSheet s=ViewSheet.Create(doc,block);s.SheetNumber=number;s.Name=name;
            Viewport p=Viewport.Create(doc,s.Id,view.Id,new XYZ(M(.135),M(.15),0));ElementType pt=doc.GetElement(p.GetTypeId()) as ElementType;Parameter show=pt==null?null:pt.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);if(show!=null&&!show.IsReadOnly)show.Set(0);
            FitViewportToSheet(doc,p,view);
            GraphicsStyle border=GetLineStyle(doc,"BIM-ANOTACOES",new Color(53,59,66),2);
            DrawLine(doc,s,.010,.010,.410,.010,border,0);DrawLine(doc,s,.410,.010,.410,.287,border,0);DrawLine(doc,s,.410,.287,.010,.287,border,0);DrawLine(doc,s,.010,.287,.010,.010,border,0);DrawLine(doc,s,.010,.060,.410,.060,border,0);DrawLine(doc,s,.225,.010,.225,.060,border,0);DrawLine(doc,s,.320,.010,.320,.060,border,0);DrawLine(doc,s,.390,.010,.390,.060,border,0);
            ElementId small=GetFooterText(doc,text);TextNote.Create(doc,s.Id,new XYZ(M(.255),M(.246),0),M(.143),notes,text);
            TextNote.Create(doc,s.Id,new XYZ(M(.018),M(.043),0),"PORTFOLIO BIM | " + spec.Title.ToUpperInvariant(),small);TextNote.Create(doc,s.Id,new XYZ(M(.018),M(.022),0),"Estudo demonstrativo - Revit 2027 - nao executivo",small);
            TextNote.Create(doc,s.Id,new XYZ(M(.232),M(.043),0),"PRJ-BIM-"+spec.Number.ToString("00")+" | "+spec.Title.ToUpperInvariant(),small);TextNote.Create(doc,s.Id,new XYZ(M(.232),M(.022),0),"ELABORADO POR: Rogério Eduardo Moreira Menegueli",small);
            TextNote.Create(doc,s.Id,new XYZ(M(.327),M(.043),0),number,small);TextNote.Create(doc,s.Id,new XYZ(M(.327),M(.022),0),name.ToUpperInvariant(),small);TextNote.Create(doc,s.Id,new XYZ(M(.395),M(.032),0),"A3",small);return s.Id;
        }

        private static void FitViewportToSheet(Document doc,Viewport viewport,View view)
        {
            doc.Regenerate();double maxWidth=M(.216),maxHeight=M(.184);
            for(int i=0;i<10;i++)
            {
                Outline box=viewport.GetBoxOutline();double width=box.MaximumPoint.X-box.MinimumPoint.X,height=box.MaximumPoint.Y-box.MinimumPoint.Y;
                double factor=Math.Max(width/maxWidth,height/maxHeight);if(factor<=1.001)break;
                int next=(int)Math.Ceiling(view.Scale*factor*1.02);if(next<=view.Scale)next=view.Scale+1;view.Scale=next;doc.Regenerate();
            }
            doc.Regenerate();viewport.SetBoxCenter(new XYZ(M(.132),M(.164),0));
            Outline fitted=viewport.GetBoxOutline();
            if(fitted.MaximumPoint.X-fitted.MinimumPoint.X>maxWidth*1.005||fitted.MaximumPoint.Y-fitted.MinimumPoint.Y>maxHeight*1.005)
                Log("VIEWPORT_FIT_WARNING "+view.Name+" scale="+view.Scale);
        }

        private static int PlanScale(double w,double d){double needed=Math.Max((w+4.0)/.205,(d+4.0)/.172);return Math.Max(50,(int)(Math.Ceiling(needed/25.0)*25));}
        private static Level FindOrCreateLevel(Document doc,double meters,string name){double e=M(meters);Level l=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x=>Math.Abs(x.Elevation-e)).FirstOrDefault();if(l==null||Math.Abs(l.Elevation-e)>M(.02))l=Level.Create(doc,e);try{l.Name=name;}catch{}return l;}
        private static Wall AddWall(Document doc,WallType type,Level level,double x1,double y1,double x2,double y2,double h)
        {
            Wall wall=Wall.Create(doc,Line.CreateBound(XY(x1,y1,level.Elevation),XY(x2,y2,level.Elevation)),type.Id,level.Id,h,0,false,false);
            if(_wallAuditProject=="07"||_wallAuditProject=="09")
            {
                string description="WALL_ELEMENT "+_wallAuditProject+" id="+wall.Id.Value+" level="+level.Name+" segment="+x1+","+y1+" to "+x2+","+y2;
                _wallAuditDescriptions[wall.Id.Value]=description;Log(description);
            }
            return wall;
        }
        private static void Open(Document doc,Wall wall,double x1,double y1,double x2,double y2,double z1,double z2){try{Level l=doc.GetElement(wall.LevelId) as Level;double b=l==null?0:l.Elevation;doc.Create.NewOpening(wall,new XYZ(M(x1),M(y1),b+M(z1)),new XYZ(M(x2),M(y2),b+M(z2)));}catch(Exception ex){Log("OPENING_NOTE "+ex.Message);}}
        private static CurveLoop RectangleLoop(double x1,double y1,double x2,double y2,double z){CurveLoop c=new CurveLoop();c.Append(Line.CreateBound(XY(x1,y1,z),XY(x2,y1,z)));c.Append(Line.CreateBound(XY(x2,y1,z),XY(x2,y2,z)));c.Append(Line.CreateBound(XY(x2,y2,z),XY(x1,y2,z)));c.Append(Line.CreateBound(XY(x1,y2,z),XY(x1,y1,z)));return c;}
        private static void AddBoxSolid(Document doc,string name,double x,double y,double w,double d,double z,double h,Material material=null)
        {
            try{var solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{RectangleLoop(x,y,x+w,y+d,M(z))},XYZ.BasisZ,M(h));DirectShape ds=DirectShape.CreateElement(doc,new ElementId(BuiltInCategory.OST_GenericModel));ds.ApplicationId="PortfolioBIM";ds.ApplicationDataId=Guid.NewGuid().ToString("N");ds.SetShape(new List<GeometryObject>{solid});try{ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(name);}catch{}if(material!=null)try{Parameter p=ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);if(p!=null&&!p.IsReadOnly)p.Set(material.Id);}catch{}}
            catch(Exception ex){Log("EQUIPMENT_NOTE "+name+" "+ex.Message);}
        }

        private static void AddProjectEquipment(Document doc,ProjectSpec spec,Equipment e,double z,Material equipment,Material metal)
        {
            string n=e.Name.ToLowerInvariant();double h=Math.Max(.35,e.H),x=e.X,y=e.Y,w=e.W,d=e.D;
            if(n.Contains("transformador"))
            {
                AddBoxSolid(doc,e.Name+" - base",x-.18,y-.18,w+.36,d+.36,z,.2,metal);
                AddBoxSolid(doc,e.Name+" - cuba",x+.15,y+.2,w-.3,d-.4,z+.2,h*.65,equipment);
                for(int i=0;i<5;i++)AddBoxSolid(doc,e.Name+" - radiador "+(i+1),x+.12+i*(w-.35)/5.0,y-.06,.10,.16,z+.28,h*.48,metal);
                for(int i=0;i<3;i++)AddCylinderSolid(doc,e.Name+" - isolador "+(i+1),x+w*.25+i*w*.22,y+d*.55,.22,.22,z+h*.65,h*.28,glassForEquipment(doc,spec.Number));
            }
            else if(n.Contains("rack")||n.Contains("bess"))
            {
                AddBoxSolid(doc,e.Name+" - estrutura",x,y,w,d,z,h,metal);
                for(int i=1;i<5;i++)AddBoxSolid(doc,e.Name+" - bandeja "+i,x+.04,y+.04,w-.08,d-.08,z+h*i/5.0,.045,equipment);
                AddBoxSolid(doc,e.Name+" - porta frontal",x+w*.08,y-.035,w*.84,.04,z+.08,h*.82,equipment);
            }
            else if(n.Contains("carregador"))
            {
                AddBoxSolid(doc,e.Name+" - pedestal",x+.12,y+.10,w*.68,d*.65,z,.18,metal);
                AddBoxSolid(doc,e.Name+" - gabinete",x+.10,y+.08,w*.72,d*.68,z+.18,h*.65,equipment);
                AddBoxSolid(doc,e.Name+" - display",x+w*.20,y+.035,w*.48,.04,z+h*.52,.20,glassForEquipment(doc,spec.Number));
                AddBoxSolid(doc,e.Name+" - base",x,y,w,d,z,.08,metal);
            }
            else if(n.Contains("esteira"))
            {
                AddBoxSolid(doc,e.Name+" - leito",x,y,w,d,z+h*.62,.12,metal);
                for(int i=0;i<4;i++)AddBoxSolid(doc,e.Name+" - suporte "+i,x+i*w/4.0,y+d*.35,.12,d*.3,z,.62*h,metal);
                AddBoxSolid(doc,e.Name+" - carga",x+w*.38,y+d*.12,w*.18,d*.76,z+h*.76,.25,equipment);
            }
            else if(n.Contains("gerador"))
            {
                AddBoxSolid(doc,e.Name+" - base antivibratoria",x-.12,y-.12,w+.24,d+.24,z,.18,metal);
                AddBoxSolid(doc,e.Name+" - alternador",x+w*.36,y+d*.15,w*.48,d*.70,z+.18,h*.72,equipment);
                AddBoxSolid(doc,e.Name+" - radiador",x+w*.05,y+d*.18,w*.22,d*.64,z+.22,h*.65,metal);
                AddCylinderSolid(doc,e.Name+" - escapamento",x+w*.78,y+d*.18,.22,.22,z+h*.65,h*.5,metal);
            }
            else if(n.Contains("bomba"))
            {
                AddBoxSolid(doc,e.Name+" - base",x-.08,y-.08,w+.16,d+.16,z,.14,metal);
                AddCylinderSolid(doc,e.Name+" - caracol",x+w*.2,y+d*.2,w*.6,d*.6,z+.14,Math.Max(.35,h*.55),equipment);
                AddBoxSolid(doc,e.Name+" - motor",x+w*.64,y+d*.22,w*.30,d*.56,z+.18,h*.42,metal);
            }
            else
            {
                AddBoxSolid(doc,e.Name+" - base",x-.06,y-.06,w+.12,d+.12,z,.10,metal);
                AddBoxSolid(doc,e.Name,x,y,w,d,z+.10,Math.Max(.25,h-.10),equipment);
            }
        }

        private static Material glassForEquipment(Document doc,int number)
        {return new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(x=>x.Name=="BIM-"+number.ToString("00")+"-Vidro");}

        private static void AddVehicle(Document doc,string name,double x,double y,Material body,Material glass,Material metal)
        {
            AddBoxSolid(doc,name+" - carroceria",x,y,4.35,1.85,.28,.88,body);
            AddBoxSolid(doc,name+" - cabine",x+.82,y+.15,2.62,1.55,1.08,.58,metal);
            AddBoxSolid(doc,name+" - para-brisa",x+1.03,y+.18,.92,1.49,1.18,.34,glass);
            AddBoxSolid(doc,name+" - vidro traseiro",x+2.18,y+.18,.95,1.49,1.18,.34,glass);
            foreach(double wx in new[]{x+.48,x+3.25})foreach(double wy in new[]{y-.08,y+1.58})AddBoxSolid(doc,name+" - roda",wx,wy,.62,.34,.22,.44,metal);
        }

        private static Material GetOrCreateMaterial(Document doc,string name,Color color)
        {
            Material m=new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(x=>x.Name==name);
            if(m==null)m=doc.GetElement(Material.Create(doc,name)) as Material;m.Color=color;m.Transparency=0;return m;
        }

        private static T MaterializeHostType<T>(Document doc,T source,string name,Material material) where T:HostObjAttributes
        {
            T result=source.Duplicate(name) as T;if(result==null)throw new InvalidOperationException("Could not duplicate host type: "+name);
            CompoundStructure cs=result.GetCompoundStructure();if(cs!=null&&cs.GetLayers().Count>0){cs.SetMaterialId(0,material.Id);result.SetCompoundStructure(cs);}return result;
        }

        private static Color PaletteColor(int n)
        {
            switch(n){case 1:return new Color(190,137,96);case 2:return new Color(83,116,143);case 3:return new Color(92,151,151);case 4:return new Color(104,139,105);case 5:return new Color(173,133,89);case 6:return new Color(109,116,122);case 7:return new Color(183,130,102);case 8:return new Color(132,126,157);case 9:return new Color(82,117,132);case 10:return new Color(87,135,157);case 11:return new Color(71,123,161);case 12:return new Color(132,123,106);case 13:return new Color(88,145,138);default:return new Color(73,137,143);}
        }

        private static Color Darken(Color c,double factor)=>new Color((byte)(c.Red*factor),(byte)(c.Green*factor),(byte)(c.Blue*factor));
        private static Color Lighten(Color c,double factor)=>new Color((byte)Math.Min(255,c.Red+(255-c.Red)*factor),(byte)Math.Min(255,c.Green+(255-c.Green)*factor),(byte)Math.Min(255,c.Blue+(255-c.Blue)*factor));

        private enum RoofForm { Gable, FlatParapet, Butterfly, Shed, MultiGable, Barrel, Sawtooth }

        private static RoofForm RoofFormFor(int n)
        {
            switch (n)
            {
                case 1: return RoofForm.Gable;
                case 2: return RoofForm.Butterfly;
                case 3: return RoofForm.Shed;
                case 4: return RoofForm.MultiGable;
                case 5: return RoofForm.Barrel;
                case 6: return RoofForm.Sawtooth;
                default: return RoofForm.FlatParapet;
            }
        }

        private static string RoofDescription(int n)
        {
            switch(n)
            {
                case 1:return "cobertura residencial em duas águas";
                case 2:return "cobertura borboleta com calha central";
                case 3:return "cobertura clínica inclinada tipo shed";
                case 4:return "coberturas múltiplas sobre as alas escolares";
                case 5:return "cobertura curva tipo abóbada";
                case 6:return "cobertura industrial dente de serra";
                case 7:return "cobertura plana com platibanda e áreas técnicas";
                case 8:return "cobertura plana com terraço técnico e pérgola";
                case 9:return "cobertura plana com equipamentos de climatização";
                default:return "cobertura plana com platibanda";
            }
        }

        private static double WallHeight(ProjectSpec s)
        {
            switch (s.Number)
            {
                case 3: return 4.2;
                case 5: return 8.0;
                case 6: return 6.2;
                case 7: return 5.2;
                case 9: return 3.35;
                default: return 3.35;
            }
        }

        private static double RoofSlopeDegrees(int n)
        {switch(n){case 1:return 22;case 3:return 8;case 4:return 17;case 6:return 13;default:return 0;}}

        private static double ModelHeight(ProjectSpec s)
        {
            if(s.Enclosed)
            {
                RoofForm form=RoofFormFor(s.Number);
                double rise=form==RoofForm.Gable||form==RoofForm.MultiGable?Math.Tan(Math.Max(10,RoofSlopeDegrees(s.Number))*Math.PI/180.0)*(s.Depth+.7)/6.0:
                    form==RoofForm.Barrel||form==RoofForm.Sawtooth?1.8:form==RoofForm.Shed?1.25:1.55;
                return (s.Floors-1)*3.6+WallHeight(s)+rise+1.6;
            }
            return s.Number==11||s.Number==14?7.0:6.0;
        }

        private static void CreateLegacyRoof(Document doc,Level level,ProjectSpec s,Material material,Material facadeMaterial)
        {
            double overhang=.3,eaveZ=level.Elevation+M(3.35),slope=RoofSlopeDegrees(s.Number),thickness=.18;
            if(slope<=0)AddBoxSolid(doc,"Cobertura BIM "+s.Number.ToString("00"),-overhang,-overhang,s.Width+2*overhang,s.Depth+2*overhang,UnitUtils.ConvertFromInternalUnits(level.Elevation,UnitTypeId.Meters)+3.35,thickness,material);
            else
            {
                double y0=M(-overhang),y1=M(s.Depth+overhang),ym=(y0+y1)/2.0,x=M(-overhang),rise=Math.Tan(slope*Math.PI/180.0)*(s.Depth+2*overhang)/2.0,peak=eaveZ+M(rise),t=M(thickness);CurveLoop profile=new CurveLoop();XYZ p0=new XYZ(x,y0,eaveZ),p1=new XYZ(x,ym,peak),p2=new XYZ(x,y1,eaveZ),p3=new XYZ(x,y1,eaveZ-t),p4=new XYZ(x,ym,peak-t),p5=new XYZ(x,y0,eaveZ-t);profile.Append(Line.CreateBound(p0,p1));profile.Append(Line.CreateBound(p1,p2));profile.Append(Line.CreateBound(p2,p3));profile.Append(Line.CreateBound(p3,p4));profile.Append(Line.CreateBound(p4,p5));profile.Append(Line.CreateBound(p5,p0));Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{profile},XYZ.BasisX,M(s.Width+2*overhang));DirectShape ds=DirectShape.CreateElement(doc,new ElementId(BuiltInCategory.OST_GenericModel));ds.ApplicationId="PortfolioBIM";ds.ApplicationDataId=Guid.NewGuid().ToString("N");ds.SetShape(new List<GeometryObject>{solid});try{ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("Cobertura em duas aguas");}catch{}try{Parameter p=ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);if(p!=null&&!p.IsReadOnly)p.Set(material.Id);}catch{}AddGableEnd(doc,"Empena oeste",-overhang,y0,y1,eaveZ,rise,facadeMaterial);AddGableEnd(doc,"Empena leste",s.Width+overhang-.12,y0,y1,eaveZ,rise,facadeMaterial);
            }
            Log("ROOF_CREATED "+s.Number.ToString("00")+" style="+(slope>0?"gable":"flat")+" slope="+slope);
        }

        private static void AddGableEnd(Document doc,string name,double x,double y0,double y1,double eaveZ,double rise,Material material)
        {
            try{double xi=M(x),ym=(y0+y1)/2.0;CurveLoop tri=new CurveLoop();XYZ a=new XYZ(xi,y0,eaveZ),b=new XYZ(xi,ym,eaveZ+M(rise)),c=new XYZ(xi,y1,eaveZ);tri.Append(Line.CreateBound(a,b));tri.Append(Line.CreateBound(b,c));tri.Append(Line.CreateBound(c,a));Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{tri},XYZ.BasisX,M(.12));DirectShape ds=DirectShape.CreateElement(doc,new ElementId(BuiltInCategory.OST_GenericModel));ds.ApplicationId="PortfolioBIM";ds.ApplicationDataId=Guid.NewGuid().ToString("N");ds.SetShape(new List<GeometryObject>{solid});try{ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(name);}catch{}try{Parameter p=ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);if(p!=null&&!p.IsReadOnly)p.Set(material.Id);}catch{}}
            catch(Exception ex){Log("GABLE_END_NOTE "+name+" "+ex.Message);}
        }

        private static void CreateRoof(Document doc,Level level,ProjectSpec s,Material roof,Material facade,Material glass,Material metal)
        {
            double overhang=.38,baseElevation=UnitUtils.ConvertFromInternalUnits(level.Elevation,UnitTypeId.Meters),eave=baseElevation+WallHeight(s),eaveZ=M(eave);
            RoofForm form=RoofFormFor(s.Number);
            if(form==RoofForm.Gable)
            {
                double rise=Math.Tan(RoofSlopeDegrees(s.Number)*Math.PI/180.0)*(s.Depth+2*overhang)/2.0;
                AddGableRoof(doc,"Cobertura residencial em duas aguas",-overhang,s.Width+2*overhang,-overhang,s.Depth+overhang,eaveZ,rise,roof,facade);
            }
            else if(form==RoofForm.Butterfly)
            {
                double ym=s.Depth/2.0;
                AddRoofProfile(doc,"Cobertura borboleta corporativa",-overhang,s.Width+2*overhang,
                    new List<XYZ>{new XYZ(M(-overhang),M(-overhang),eaveZ+M(.55)),new XYZ(M(-overhang),M(ym),eaveZ),new XYZ(M(-overhang),M(s.Depth+overhang),eaveZ+M(.55))},.18,roof);
                AddBoxSolid(doc,"Calha central da cobertura",-.2,ym-.12,s.Width+.4,.24,eave-.06,.16,metal);
            }
            else if(form==RoofForm.Shed)
            {
                AddRoofProfile(doc,"Cobertura inclinada da clinica",-overhang,s.Width+2*overhang,
                    new List<XYZ>{new XYZ(M(-overhang),M(-overhang),eaveZ),new XYZ(M(-overhang),M(s.Depth+overhang),eaveZ+M(1.25))},.18,roof);
                for(int i=0;i<3;i++)AddBoxSolid(doc,"Clerestory clinica "+(i+1),2+i*7.0,s.Depth*.42,.15,s.Depth*.34,eave+.48,.58,glass);
            }
            else if(form==RoofForm.MultiGable)
            {
                double bay=s.Depth/3.0;
                for(int i=0;i<3;i++)AddGableRoof(doc,"Cobertura da ala escolar "+(i+1),-.15,s.Width+.3,i*bay,(i+1)*bay,eaveZ,1.55,roof,facade);
                AddBoxSolid(doc,"Lanternim da biblioteca",s.Width*.35,s.Depth*.31,s.Width*.30,s.Depth*.10,eave+1.35,.55,glass);
            }
            else if(form==RoofForm.Barrel)
            {
                List<XYZ> arch=new List<XYZ>();double y0=-overhang,y1=s.Depth+overhang,rise=2.0;
                for(int i=0;i<=16;i++){double t=i/16.0,y=y0+(y1-y0)*t,z=eave+rise*(1-Math.Pow(2*t-1,2));arch.Add(new XYZ(M(-overhang),M(y),level.Elevation+M(z)));}
                AddRoofProfile(doc,"Cobertura curva do galpao",-overhang,s.Width+2*overhang,arch,.18,roof);
                for(int i=0;i<4;i++)AddBoxSolid(doc,"Faixa zenital do galpao "+(i+1),4+i*8.0,s.Depth*.42,.9,s.Depth*.12,eave+1.6,.10,glass);
            }
            else if(form==RoofForm.Sawtooth)
            {
                List<XYZ> teeth=new List<XYZ>();double yStart=-overhang,bay=(s.Depth+2*overhang)/4.0,rise=1.75;
                teeth.Add(new XYZ(M(-overhang),M(yStart),eaveZ));
                for(int i=0;i<4;i++){double y=yStart+i*bay;teeth.Add(new XYZ(M(-overhang),M(y+bay*.72),eaveZ+M(rise)));teeth.Add(new XYZ(M(-overhang),M(y+bay*.78),eaveZ+M(rise)));teeth.Add(new XYZ(M(-overhang),M(y+bay),eaveZ));}
                AddRoofProfile(doc,"Cobertura industrial dente de serra",-overhang,s.Width+2*overhang,teeth,.18,roof);
                for(int i=0;i<4;i++){double y=yStart+(i+.73)*bay;AddBoxSolid(doc,"Lanternim oficina "+(i+1),-.15,y,s.Width+.3,.10,eave+1.15,.55,glass);}
            }
            else
            {
                AddBoxSolid(doc,"Cobertura plana impermeabilizada",-overhang,-overhang,s.Width+2*overhang,s.Depth+2*overhang,eave,.22,roof);
                double top=eave+.2,p=.16,h=.65;
                AddBoxSolid(doc,"Platibanda frontal",-.15,-overhang,s.Width+.3,p,top,h,facade);
                AddBoxSolid(doc,"Platibanda posterior",-.15,s.Depth+overhang-p,s.Width+.3,p,top,h,facade);
                AddBoxSolid(doc,"Platibanda lateral oeste",-overhang,-overhang,p,s.Depth+2*overhang,top,h,facade);
                AddBoxSolid(doc,"Platibanda lateral leste",s.Width+overhang-p,-overhang,p,s.Depth+2*overhang,top,h,facade);
            }
            double riseTop=form==RoofForm.Barrel?2.0:form==RoofForm.Sawtooth?1.75:form==RoofForm.MultiGable?1.55:form==RoofForm.Gable?Math.Tan(RoofSlopeDegrees(s.Number)*Math.PI/180.0)*(s.Depth+2*overhang)/2.0:form==RoofForm.Butterfly?.55:form==RoofForm.Shed?1.25:.9;
            AddRoofscape(doc,s,eave+riseTop,roof,glass,metal);
            Log("ROOF_CREATED "+s.Number.ToString("00")+" style="+form+" rise="+riseTop.ToString("0.0"));
        }

        private static void AddGableRoof(Document doc,string name,double x,double length,double y0,double y1,double eaveZ,double rise,Material roof,Material facade)
        {
            double ym=(y0+y1)/2.0;
            AddRoofProfile(doc,name,x,length,new List<XYZ>{new XYZ(M(x),M(y0),eaveZ),new XYZ(M(x),M(ym),eaveZ+M(rise)),new XYZ(M(x),M(y1),eaveZ)},.18,roof);
            AddGableEnd(doc,name+" - empena oeste",x,M(y0),M(y1),eaveZ,rise,facade);
            AddGableEnd(doc,name+" - empena leste",x+length-.12,M(y0),M(y1),eaveZ,rise,facade);
        }

        private static void AddRoofProfile(Document doc,string name,double x,double length,List<XYZ> upper,double thickness,Material material)
        {
            try
            {
                List<XYZ> polygon=new List<XYZ>(upper);
                for(int i=upper.Count-1;i>=0;i--)polygon.Add(new XYZ(upper[i].X,upper[i].Y,upper[i].Z-M(thickness)));
                CurveLoop loop=new CurveLoop();
                for(int i=0;i<polygon.Count;i++){XYZ a=polygon[i],b=polygon[(i+1)%polygon.Count];if(a.DistanceTo(b)>M(.001))loop.Append(Line.CreateBound(a,b));}
                Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{loop},XYZ.BasisX,M(length));
                AddSolidDirectShape(doc,name,solid,material);
            }
            catch(Exception ex){Log("ROOF_PROFILE_NOTE "+name+" "+ex.Message);}
        }

        private static void AddRoofscape(Document doc,ProjectSpec s,double top,Material roof,Material glass,Material metal)
        {
            if(s.Number==7)
            {
                for(int i=0;i<3;i++)AddBoxSolid(doc,"Skylight comercial "+(i+1),5+i*8,5,3.2,2.2,top-.28,.22,glass);
                for(int i=0;i<3;i++)AddBoxSolid(doc,"Unidade de climatizacao "+(i+1),23+i*2.2,16,1.6,1.4,top-.25,1.0,metal);
            }
            else if(s.Number==8)
            {
                Material facade=facadeMaterialForRoof(doc,s.Number);
                AddBoxSolid(doc,"Volume tecnico e lobby do terraco",7.5,5.0,7,5.5,top-.65,1.7,facade);
                for(int i=0;i<4;i++)AddBoxSolid(doc,"Pilar da pergola do terraco "+(i+1),8.0+(i%2)*5.5,5.6+(i/2)*3.7, .16,.16,top+.1,1.3,metal);
                for(int i=0;i<6;i++)AddBoxSolid(doc,"Lamela da pergola do terraco "+(i+1),7.9,5.55+i*.75,6.1,.10,top+1.35,.10,metal);
            }
            else if(s.Number==9)
            {
                for(int i=0;i<4;i++){double x=5+i*6.0;AddBoxSolid(doc,"Chiller de precisao "+(i+1),x,8,3.8,3.0,top-.35,1.15,metal);AddCylinderSolid(doc,"Ventilador do chiller "+(i+1),x+.5,8.5,1.0,1.0,top+.82,.12,glass);AddCylinderSolid(doc,"Ventilador do chiller "+(i+1)+" B",x+2.0,8.5,1.0,1.0,top+.82,.12,glass);}
                AddBoxSolid(doc,"Tela tecnica de cobertura",3,7.6,25,.12,top+1.0,.8,facadeMaterialForRoof(doc,s.Number));
            }
            else if(s.Number==2)
            {
                AddBoxSolid(doc,"Volume tecnico e acesso ao terraco",14,9,4.5,3.0,top-.2,1.1,metal);
            }
        }

        private static Material facadeMaterialForRoof(Document doc,int number)
        {return new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(x=>x.Name=="BIM-"+number.ToString("00")+"-Fachada");}

        private static void AddSolidDirectShape(Document doc,string name,Solid solid,Material material,double xOffset=0)
        {
            DirectShape ds=DirectShape.CreateElement(doc,new ElementId(BuiltInCategory.OST_GenericModel));ds.ApplicationId="PortfolioBIM";ds.ApplicationDataId=Guid.NewGuid().ToString("N");
            GeometryObject shape=solid;if(Math.Abs(xOffset)>.0001)shape=SolidUtils.CreateTransformed(solid,Transform.CreateTranslation(new XYZ(M(xOffset),0,0)));
            ds.SetShape(new List<GeometryObject>{shape});try{ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(name);}catch{}
            try{Parameter p=ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);if(p!=null&&!p.IsReadOnly)p.Set(material.Id);}catch{}
        }

        private static void AddBuildingFeatures(Document doc,ProjectSpec s,Material facade,Material roof,Material glass,Material metal,Material equipment)
        {
            double w=s.Width,d=s.Depth,h=WallHeight(s);
            switch(s.Number)
            {
                case 1:
                    AddCanopy(doc,"Varanda de entrada residencial",4.1,-1.65,5.0,1.55,2.75,roof);
                    AddBoxSolid(doc,"Portao da garagem",.6,-.12,3.8,.08,.12,2.15,metal);
                    break;
                case 2:
                    for(int f=0;f<s.Floors;f++)
                    {
                        double z=f*3.6+.78;AddBoxSolid(doc,"Pele envidracada do escritorio pavimento "+(f+1),1.0,-.12,w-2.0,.08,z,1.75,glass);
                        for(int i=0;i<=6;i++)AddBoxSolid(doc,"Montante de fachada "+(f+1)+"-"+i,1.0+i*(w-2.0)/6.0,-.16,.075,.16,z,.15,metal);
                    }
                    AddCanopy(doc,"Marquise principal do escritorio",5.5,-2.05,9.0,1.9,3.05,metal);
                    break;
                case 3:
                    AddBoxSolid(doc,"Fachada envidracada da recepcao",1.8,-.12,8.2,.08,.55,2.8,glass);
                    AddCanopy(doc,"Marquise de acesso clinico",5.5,-2.15,12.5,2.0,3.25,roof);
                    AddBoxSolid(doc,"Portal de acesso e ambulancia",4.8,-2.25,.18,.18,.15,3.0,metal);
                    break;
                case 4:
                    for(int i=0;i<6;i++)AddBoxSolid(doc,"Brise de sala de aula "+(i+1),2.2+i*4.2,-.11,1.4,.08,1.05,1.55,glass);
                    AddCanopy(doc,"Galeria coberta da escola",8,-2.0,14,2.0,3.05,roof);
                    AddBoxSolid(doc,"Portal colorido da escola",10,-.28,10,.18,0,3.2,metal);
                    break;
                case 5:
                    for(int i=0;i<5;i++)
                    {
                        double x=2.5+i*6.4;AddBoxSolid(doc,"Porta seccional de doca "+(i+1),x,-.12,4.2,.10,.25,5.6,metal);
                        AddCanopy(doc,"Marquise de doca "+(i+1),x-.35,-1.25,4.9,1.25,5.2,roof);
                    }
                    AddBoxSolid(doc,"Faixa envidracada da administracao",1.0,-.13,8,.08,5.7,1.15,glass);
                    break;
                case 6:
                    for(int i=0;i<4;i++)AddBoxSolid(doc,"Portao de manutencao "+(i+1),1.3+i*7.1,-.13,5.4,.10,.2,4.6,metal);
                    AddBoxSolid(doc,"Faixa envidracada do escritorio tecnico",2,-.14,8,.08,4.85,1.0,glass);
                    AddBoxSolid(doc,"Trilho de ponte rolante sul",1,1,w-2,.12,5.35,.18,metal);
                    AddBoxSolid(doc,"Trilho de ponte rolante norte",1,d-1.12,w-2,.12,5.35,.18,metal);
                    break;
                case 7:
                    AddBoxSolid(doc,"Vitrine continua do supermercado",4,-.13,19,.09,.6,2.85,glass);
                    AddCanopy(doc,"Marquise da entrada comercial",6,-2.25,18,2.15,3.15,roof);
                    AddBoxSolid(doc,"Testeira de identificacao comercial",4,-.24,19,.12,3.35,.72,metal);
                    break;
                case 8:
                    AddCanopy(doc,"Marquise do lobby do hotel",5.0,-2.0,12.0,1.9,3.0,roof);
                    for(int f=1;f<s.Floors;f++)
                    {
                        double z=f*3.6-.12;AddBoxSolid(doc,"Sacada corrida pavimento "+(f+1),1.2,-1.15,w-2.4,1.15,z,.20,roof);
                        AddBoxSolid(doc,"Guarda-corpo pavimento "+(f+1),1.2,-1.18,w-2.4,.08,z+.20,.92,glass);
                        for(int i=1;i<6;i++)AddBoxSolid(doc,"Divisao de sacada "+f+"-"+i,1.2+i*(w-2.4)/6.0,-1.18,.06,.08,z+.20,.92,metal);
                    }
                    break;
                case 9:
                    for(int i=0;i<7;i++)AddBoxSolid(doc,"Painel de veneziana tecnica "+(i+1),1.5+i*4.2,-.13,2.1,.10,.85,2.3,metal);
                    AddCanopy(doc,"Dossel de acesso controlado ao data center",4,-1.6,8,1.5,3.0,roof);
                    AddBoxSolid(doc,"Grade de ventilacao de emergencia",3,d-.16,25,.12,1.2,2.2,glass);
                    break;
            }
        }

        private static void AddSiteShelters(Document doc,ProjectSpec s,Material roof,Material facade,Material glass,Material metal,Material solar)
        {
            if(s.Number==10)
            {
                for(int row=0;row<3;row++)AddCanopy(doc,"Cobertura solar de vagas fileira "+(row+1),3,1.4+row*9.25,36,7.2,4.0,solar);
                AddBoxSolid(doc,"Ilha de controle de acesso",1,1,.7,.6,.05,1.4,metal);
                for(int row=0;row<3;row++)for(int col=0;col<7;col++)
                {
                    if(row==1&&(col==3||col==4))continue;
                    AddVehicle(doc,"Veiculo estacionado "+(row*7+col+1),3.45+col*5.2,3.9+row*9.15,facade,glass,metal);
                }
            }
            else if(s.Number==11)
            {
                AddPavilion(doc,"Centro de operacao fotovoltaica",4,3,9,5,3.1,true,facade,roof,glass,metal);
                AddCanopy(doc,"Abrigo de inversores fotovoltaicos",49,13,10,13,3.8,solar);
            }
            else if(s.Number==12)
            {
                AddPavilion(doc,"Edificio de comando da subestacao",3.2,14.5,9.5,7.2,3.35,true,facade,roof,glass,metal);
                AddCanopy(doc,"Abrigo de transformador e manutencao",13.5,3.8,7.5,5.5,4.3,roof);
                AddCanopy(doc,"Abrigo de paineis de comando",16,13.5,13,4,3.6,roof);
            }
            else if(s.Number==13)
            {
                AddPavilion(doc,"Casa de bombas e filtragem",33.5,17.5,8,7,3.4,true,facade,roof,glass,metal);
                AddPavilion(doc,"Abrigo de dosagem e controle",33.5,3,7,5,3.0,false,facade,roof,glass,metal);
                AddCanopy(doc,"Passarela tecnica entre tanques",15.5,14.6,14,1.0,2.6,metal);
            }
            else if(s.Number==14)
            {
                AddPavilion(doc,"Casa do gerador de emergencia",4.5,4.5,7,6,3.5,true,facade,roof,glass,metal);
                AddPavilion(doc,"Enclausuramento BESS e PCS",34,18,10,7,3.8,false,facade,roof,glass,metal);
                AddCanopy(doc,"Cobertura solar de conversores",21,17.4,10,4.3,3.5,solar);
            }
        }

        private static void AddPavilion(Document doc,string name,double x,double y,double w,double d,double h,bool gable,Material facade,Material roof,Material glass,Material metal)
        {
            double t=.18,z=0;
            Material pavement=GetOrCreateMaterial(doc,name+" - Piso",Lighten(facade.Color,.76));
            AddBoxSolid(doc,name+" - piso",x,y,w,d,z,.16,pavement);
            AddBoxSolid(doc,name+" - parede oeste",x,y,t,d,.16,h-.16,facade);
            AddBoxSolid(doc,name+" - parede leste",x+w-t,y,t,d,.16,h-.16,facade);
            AddBoxSolid(doc,name+" - parede posterior",x+t,y+d-t,w-2*t,t,.16,h-.16,facade);
            double opening=Math.Min(2.2,w*.32),side=(w-opening)/2.0;
            AddBoxSolid(doc,name+" - frente oeste",x,y,side,t,.16,h-.16,facade);
            AddBoxSolid(doc,name+" - frente leste",x+side+opening,y,w-side-opening,t,.16,h-.16,facade);
            AddBoxSolid(doc,name+" - verga da porta",x+side,y,opening,t,h-2.35,.28,facade);
            AddBoxSolid(doc,name+" - porta tecnica",x+side+.08,y-.035,opening-.16,.045,.16,2.1,metal);
            AddBoxSolid(doc,name+" - janela de controle",x+w*.64,y-.04,w*.22,.05,h*.52,h*.32,glass);
            double eaveZ=M(h);
            if(gable)AddGableRoof(doc,name+" - cobertura inclinada",x,w,y,y+d,eaveZ,.9,roof,facade);
            else
            {
                AddBoxSolid(doc,name+" - cobertura plana",x-.25,y-.25,w+.5,d+.5,h,.22,roof);
                AddBoxSolid(doc,name+" - platibanda frontal",x-.1,y-.25,w+.2,.15,h+.2,.45,facade);
            }
        }

        private static void AddFenceSegment(Document doc,Segment seg,Material metal)
        {
            double dx=seg.X2-seg.X1,dy=seg.Y2-seg.Y1,len=Math.Sqrt(dx*dx+dy*dy);int bays=Math.Max(1,(int)Math.Ceiling(len/3.0));
            for(int i=0;i<=bays;i++)
            {
                double t=(double)i/bays,x=seg.X1+dx*t,y=seg.Y1+dy*t;AddBoxSolid(doc,"Poste de cerca",x-.055,y-.055,.11,.11,.02,1.9,metal);
            }
            if(Math.Abs(dx)>=Math.Abs(dy))
            {
                double x=Math.Min(seg.X1,seg.X2),y=Math.Min(seg.Y1,seg.Y2)-.025;
                AddBoxSolid(doc,"Travessa de cerca inferior",x,y,len,.05,1.0,.06,metal);AddBoxSolid(doc,"Travessa de cerca superior",x,y,len,.05,1.78,.06,metal);
            }
            else
            {
                double x=Math.Min(seg.X1,seg.X2)-.025,y=Math.Min(seg.Y1,seg.Y2);
                AddBoxSolid(doc,"Travessa de cerca inferior",x,y,.05,len,1.0,.06,metal);AddBoxSolid(doc,"Travessa de cerca superior",x,y,.05,len,1.78,.06,metal);
            }
        }

        private static void AddLegacySiteShelters(Document doc,ProjectSpec s,Material material)
        {
            if(s.Number==10){AddCanopy(doc,"Estacionamento - cobertura de recarga",18,8,10,10,3.35,material);}
            else if(s.Number==11){AddCanopy(doc,"Usina FV - abrigo de inversores",49,13,10.5,15,3.5,material);}
            else if(s.Number==12){AddCanopy(doc,"Subestacao - abrigo de CCM",16,13.5,13,4,3.4,material);AddCanopy(doc,"Subestacao - cobertura tecnica do transformador",13.5,3.8,7.5,5.5,4.2,material);}
            else if(s.Number==13){AddCanopy(doc,"ETA - cobertura da casa de bombas",33.5,17.5,7.5,5.5,3.4,material);}
            else if(s.Number==14){AddCanopy(doc,"Microrede - abrigo BESS e conversao",34,18,10,6.5,3.7,material);AddCanopy(doc,"Microrede - abrigo do gerador",4.5,4.5,6.5,5.5,3.4,material);}
        }

        private static void AddCanopy(Document doc,string name,double x,double y,double w,double d,double z,Material material)
        {
            AddBoxSolid(doc,name+" - cobertura",x,y,w,d,z,.16,material);
            double col=.18;int bays=Math.Max(1,(int)Math.Ceiling(w/5.0));
            for(int i=0;i<=bays;i++)
            {
                double px=x+w*i/bays-col/2.0;
                foreach(double py in new[]{y+.2,y+d-.2-col})
                    AddBoxSolid(doc,name+" - pilar",px,py,col,col,.05,z-.08,material);
                if(i<bays)
                {
                    double bayX=x+w*i/bays,bayW=w/bays;
                    AddBoxSolid(doc,name+" - viga transversal",px,y+.16,col,d-.32,z-.20,.16,material);
                    AddBoxSolid(doc,name+" - longarina lateral",bayX,y+.16,bayW,.12,z-.20,.16,material);
                    AddBoxSolid(doc,name+" - longarina posterior",bayX,y+d-.28,bayW,.12,z-.20,.16,material);
                }
            }
            AddBoxSolid(doc,name+" - calha frontal",x,y-.08,w,.10,z-.16,.12,material);
            AddBoxSolid(doc,name+" - calha posterior",x,y+d-.02,w,.10,z-.16,.12,material);
        }

        private static void AddSolarTable(Document doc,Equipment e,double z,double rise,Material material,Material metal)
        {
            try
            {
                double x=M(e.X),y=M(e.Y),d=M(e.D),baseZ=M(z+1.0),r=M(rise),t=M(.045);CurveLoop profile=new CurveLoop();
                XYZ p0=new XYZ(x,y,baseZ),p1=new XYZ(x,y+d,baseZ+r),p2=new XYZ(x,y+d,baseZ+r+t),p3=new XYZ(x,y,baseZ+t);
                profile.Append(Line.CreateBound(p0,p1));profile.Append(Line.CreateBound(p1,p2));profile.Append(Line.CreateBound(p2,p3));profile.Append(Line.CreateBound(p3,p0));
                Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{profile},XYZ.BasisX,M(e.W));AddSolidDirectShape(doc,e.Name,solid,material);
                foreach(double px in new[]{e.X+.12,e.X+e.W-.2})foreach(double py in new[]{e.Y+.12,e.Y+e.D-.2})
                {
                    double height=1.0+(py>e.Y+e.D/2.0?rise:0)-.06;
                    AddBoxSolid(doc,e.Name+" - suporte",px,py,.08,.08,z+.05,height,metal);
                }
                AddBoxSolid(doc,e.Name+" - longarina frontal",e.X,e.Y+.06,e.W,.06,z+.94,.12,metal);
                AddBoxSolid(doc,e.Name+" - longarina posterior",e.X,e.Y+e.D-.12,e.W,.06,z+.94+rise,.12,metal);
            }
            catch(Exception ex){Log("SOLAR_TABLE_NOTE "+e.Name+" "+ex.Message);AddBoxSolid(doc,e.Name,e.X,e.Y,e.W,e.D,z,e.H,material);}
        }

        private static void AddCylinderSolid(Document doc,string name,double x,double y,double w,double d,double z,double h,Material material)
        {
            try{double r=M(Math.Min(w,d)/2.0),cx=M(x+w/2.0),cy=M(y+d/2.0),baseZ=M(z);CurveLoop loop=new CurveLoop();const int sides=24;for(int i=0;i<sides;i++){double a=2*Math.PI*i/sides,b=2*Math.PI*(i+1)/sides;loop.Append(Line.CreateBound(new XYZ(cx+r*Math.Cos(a),cy+r*Math.Sin(a),baseZ),new XYZ(cx+r*Math.Cos(b),cy+r*Math.Sin(b),baseZ)));}Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{loop},XYZ.BasisZ,M(h));DirectShape ds=DirectShape.CreateElement(doc,new ElementId(BuiltInCategory.OST_GenericModel));ds.ApplicationId="PortfolioBIM";ds.ApplicationDataId=Guid.NewGuid().ToString("N");ds.SetShape(new List<GeometryObject>{solid});try{ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(name);}catch{}try{Parameter p=ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);if(p!=null&&!p.IsReadOnly)p.Set(material.Id);}catch{}}
            catch(Exception ex){Log("TANK_NOTE "+name+" "+ex.Message);AddBoxSolid(doc,name,x,y,w,d,z,h,material);}
        }
        private static void BuildGISPhysicalLayout(Document doc,Level level,WallType wallType,Material equipment,Material metal,Material glass)
        {
            double wallHeight=M(6.2);
            AddWall(doc,wallType,level,8,8,44,8,wallHeight);
            AddWall(doc,wallType,level,44,8,44,24,wallHeight);
            AddWall(doc,wallType,level,44,24,8,24,wallHeight);
            AddWall(doc,wallType,level,8,24,8,8,wallHeight);
            AddWall(doc,wallType,level,34,8,34,24,wallHeight);
            AddWall(doc,wallType,level,8,29,20,29,M(4.0));
            AddWall(doc,wallType,level,20,29,20,39,M(4.0));
            AddWall(doc,wallType,level,20,39,8,39,M(4.0));
            AddWall(doc,wallType,level,8,39,8,29,M(4.0));

            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"GIS-ROOM-01","Edificacao GIS aberta no modelo para leitura interna; paredes nativas Revit, sem roof ou arquitetura executiva.",8,8,36,16,0.02,0.08,metal);
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"CTRL-ROOM-01","Casa de controle; volume de piso conceitual.",8,29,12,10,0.02,0.08,metal);

            string[] bayTags={"GIS-LB-01","GIS-TR-01","GIS-BC-01","GIS-TR-02","GIS-LB-02"};
            string[] bayNotes={"Bay de linha 138 kV - disjuntor e seccionadoras conceituais", "Bay de transformador TR-01 - 138 kV", "Bay de acoplamento de barras A/B - 138 kV", "Bay de transformador TR-02 - 138 kV", "Bay de linha / reserva 138 kV - uso a confirmar"};
            for(int i=0;i<bayTags.Length;i++)
            {
                double x=9.5+i*4.65;
                AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,bayTags[i],bayNotes[i]+"; envelope conceitual, sem fabricante ou conectores.",x,13.2,3.65,2.0,0.05,2.75,equipment);
                AddGISBox(doc,BuiltInCategory.OST_GenericModel,bayTags[i]+"-BUS","Barramento encapsulado conceitual; geometria indicativa.",x-.3,16.0,4.25,.24,3.05,.28,metal);
            }
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"GIS-BUS-A","Barramento 138 kV A; secao esquematica, sem dimensionamento eletrodinamico.",9.5,17.1,13.8,.25,3.45,.24,metal);
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"GIS-BUS-B","Barramento 138 kV B; secao esquematica, sem dimensionamento eletrodinamico.",24.0,17.1,8.2,.25,3.45,.24,metal);

            AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"MV-CUB-A01","Cubiculo 13,8 kV - alimentador A01; corrente e capacidade interruptiva nao selecionadas.",34.7,10.0,1.25,1.1,.05,2.55,equipment);
            for(int i=1;i<7;i++)
                AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"MV-CUB-A"+(i+1).ToString("00"),"Cubiculo 13,8 kV - alimentador / acoplamento de estudo; sem especificacao de fabricante.",34.3+i*1.32,10.0,1.15,1.1,.05,2.55,equipment);
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"MV-BUS-A","Barramento 13,8 kV A; estudo funcional.",34.6,12.2,9.3,.18,2.75,.18,metal);
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"MV-BUS-B","Barramento 13,8 kV B; estudo funcional.",34.6,13.0,9.3,.18,2.75,.18,metal);

            AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"TR-01","Transformador TR-01: 25 MVA, 138/13,8 kV, Z=8% assumida; envelope sem dados de fabricante.",24.5,29.1,5.8,4.7,.25,3.6,equipment);
            AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"TR-02","Transformador TR-02: 25 MVA, 138/13,8 kV, Z=8% assumida; envelope sem dados de fabricante.",41.0,29.1,5.8,4.7,.25,3.6,equipment);
            for(int tr=0;tr<2;tr++)
            {
                double x=24.8+tr*16.5;
                for(int i=0;i<5;i++) AddGISBox(doc,BuiltInCategory.OST_GenericModel,"TR-0"+(tr+1)+"-RAD-0"+(i+1),"Radiador conceitual do transformador; dimensoes e perdas nao informadas.",x+i*1.03,28.75,.52,.16,.65,2.1,metal);
                for(int i=0;i<3;i++) AddGISBox(doc,BuiltInCategory.OST_GenericModel,"TR-0"+(tr+1)+"-BUSH-0"+(i+1),"Bucha representativa; classe de isolamento nao definida.",x+1.1+i*1.55,33.3,.35,.35,3.85,.7,glass);
                AddGISBox(doc,BuiltInCategory.OST_GenericModel,"TR-0"+(tr+1)+"-PAD","Base de concreto conceitual do transformador.",x-.35,28.75,6.5,5.35,0.0,.25,metal);
            }

            string[] panels={"IED-LINE-01","IED-BUS-01","IED-TR-01","IED-TR-02","HMI-SCADA-01","GW-IEC61850-01"};
            for(int i=0;i<panels.Length;i++)
                AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,panels[i],"Painel de protecao / automacao; arquitetura conceitual sem fabricante, ajustes ou configuracao de rede.",9.2+(i%3)*3.0,31.0+(i/3)*3.1,.72,.60,0.05,2.15,equipment);
            AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"AUX-CA-01","Quadro de servicos auxiliares CA; potencia e alimentacao a definir.",9.2,36.2,1.15,.62,.05,2.0,equipment);
            AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"AUX-CC-01","Banco / quadro CC conceitual; tensao, capacidade e autonomia pendentes.",12.0,36.2,1.15,.62,.05,2.0,equipment);
            AddGISBox(doc,BuiltInCategory.OST_ElectricalEquipment,"UPS-01","UPS de controle conceitual; potencia e autonomia nao dimensionadas.",14.8,36.2,1.15,.62,.05,2.0,equipment);

            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"TRN-HV-01","Canaleta/caminho de cabos entre edificio GIS e patio de transformadores; sem ocupacao ou separacao calculada.",9.0,25.2,39.0,.70,.02,.55,metal);
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"TRN-CTRL-01","Canaleta conceitual de controle e protecao.",20.5,30.8,3.1,.50,.02,.35,metal);
            AddGISBox(doc,BuiltInCategory.OST_GenericModel,"YARD-AREA-01","Area de patio e afastamentos esquematicos; distancias eletricas e acessos nao verificados.",52.0,8.0,9.0,16.0,.01,.06,metal);
        }

        private static DirectShape AddGISBox(Document doc,BuiltInCategory category,string mark,string comments,double x,double y,double w,double d,double z,double h,Material material)
        {
            Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{RectangleLoop(x,y,x+w,y+d,M(z))},XYZ.BasisZ,M(h));
            DirectShape shape=DirectShape.CreateElement(doc,new ElementId(category));
            shape.ApplicationId="PortfolioBIM.GIS";
            shape.ApplicationDataId=mark+"-"+Guid.NewGuid().ToString("N");
            shape.SetShape(new List<GeometryObject>{solid});
            Parameter tag=shape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if(tag!=null&&!tag.IsReadOnly)tag.Set(mark);
            Parameter note=shape.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if(note!=null&&!note.IsReadOnly)note.Set(comments);
            Parameter materialParameter=shape.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
            if(material!=null&&materialParameter!=null&&!materialParameter.IsReadOnly)materialParameter.Set(material.Id);
            return shape;
        }

        private static void DrawGISSingleLine(Document doc,ViewDrafting view,ElementId text,GraphicsStyle line,GraphicsStyle power)
        {
            TextNote.Create(doc,view.Id,XY(.2,10.1,0),"UNIFILAR FUNCIONAL | BARRAS 138 / 13,8 kV | ESTUDO",text);
            Box(doc,view,.8,8.2,4.4,9.25,"REDE 138 kV",text,line,0);
            Box(doc,view,6.0,8.2,9.6,9.25,"BAY LINHA",text,power,0);
            DrawLine(doc,view,4.4,8.72,6.0,8.72,line,0);
            DrawLine(doc,view,7.8,8.2,7.8,7.35,line,0);
            TextNote.Create(doc,view.Id,XY(2.7,7.25,0),"BARRA A 138 kV",text);
            TextNote.Create(doc,view.Id,XY(12.7,7.25,0),"BARRA B 138 kV",text);
            DrawLine(doc,view,1.2,6.95,8.5,6.95,line,0);
            DrawLine(doc,view,10.6,6.95,17.9,6.95,line,0);
            Box(doc,view,8.7,6.45,10.4,7.45,"BC",text,power,0);
            DrawLine(doc,view,8.5,6.95,8.7,6.95,line,0);
            DrawLine(doc,view,10.4,6.95,10.6,6.95,line,0);
            Box(doc,view,3.0,5.2,7.4,6.15,"BAY TR-01",text,power,0);
            Box(doc,view,11.7,5.2,16.1,6.15,"BAY TR-02",text,power,0);
            DrawLine(doc,view,5.2,6.95,5.2,6.15,line,0);
            DrawLine(doc,view,13.9,6.95,13.9,6.15,line,0);
            Box(doc,view,2.5,3.55,7.9,4.65,"TR-01 | 25 MVA\n138/13,8 kV",text,power,0);
            Box(doc,view,11.2,3.55,16.6,4.65,"TR-02 | 25 MVA\n138/13,8 kV",text,power,0);
            DrawLine(doc,view,5.2,5.2,5.2,4.65,power,0);
            DrawLine(doc,view,13.9,5.2,13.9,4.65,power,0);
            Box(doc,view,2.5,1.65,7.9,2.7,"BARRA 13,8 kV A\nCUBICULOS MT",text,power,0);
            Box(doc,view,11.2,1.65,16.6,2.7,"BARRA 13,8 kV B\nCUBICULOS MT",text,power,0);
            DrawLine(doc,view,5.2,3.55,5.2,2.7,power,0);
            DrawLine(doc,view,13.9,3.55,13.9,2.7,power,0);
            TextNote.Create(doc,view.Id,XY(.6,.8,0),"Alimentadores, disjuntores, TCs/TPs e ajustes pendentes de dados de rede e fabricante.",text);
        }

        private static void DrawGISProtectionArchitecture(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power,GraphicsStyle data)
        {
            TextNote.Create(doc,view.Id,XY(.4,10.0,0),"PROTECAO, CONTROLE E SERVICOS AUXILIARES | ARQUITETURA",text);
            Box(doc,view,.5,7.4,5.2,8.9,"IEDs GIS\nLINHA / BARRA / TR",text,power,0);
            Box(doc,view,6.5,7.4,11.2,8.9,"REDE / FIBRA\nCOMUNICACAO",text,data,0);
            Box(doc,view,12.5,7.4,17.2,8.9,"HMI / SCADA\nGATEWAY",text,data,0);
            DrawLine(doc,view,5.2,8.15,6.5,8.15,data,0);
            DrawLine(doc,view,11.2,8.15,12.5,8.15,data,0);
            Box(doc,view,.5,4.55,5.2,6.05,"TC / TP\nMEDIDAS",text,note,0);
            Box(doc,view,6.5,4.55,11.2,6.05,"TRIP / DJ\nCOMANDO",text,power,0);
            Box(doc,view,12.5,4.55,17.2,6.05,"TEMPO / EVENTOS\nSINCRONISMO",text,data,0);
            DrawLine(doc,view,2.85,7.4,2.85,6.05,note,0);
            DrawLine(doc,view,5.2,5.3,6.5,5.3,power,0);
            DrawLine(doc,view,14.85,7.4,14.85,6.05,data,0);
            Box(doc,view,.5,1.65,5.2,3.15,"CARREGADOR\nCC",text,note,0);
            Box(doc,view,6.5,1.65,11.2,3.15,"BATERIA CC\nAUTONOMIA PENDENTE",text,note,0);
            Box(doc,view,12.5,1.65,17.2,3.15,"UPS / AUX CA\nPOTENCIA PENDENTE",text,note,0);
            DrawLine(doc,view,5.2,2.4,6.5,2.4,note,0);
            TextNote.Create(doc,view.Id,XY(.5,.75,0),"Topologia, protocolo, ajustes, redundancia e autonomia dependem de dados de rede e fabricante.",text);
        }

        private static void DrawGISCalculationSummary(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power,GraphicsStyle data)
        {
            string s=GisValue("normal_load","apparent_mva","0.00");
            string q=GisValue("normal_load","reactive_mvar","0.00");
            string ip=GisValue("normal_load","current_primary_ka","0.000");
            string isec=GisValue("normal_load","current_secondary_ka","0.000");
            string loading=GisValue("normal_load","transformer_loading_pct","0.0");
            string n1=GisValue("n_minus_one","one_transformer_critical_loading_pct","0.0");
            string n1full=GisValue("n_minus_one","one_transformer_full_load_pct","0.0");
            string comp=GisValue("power_factor_correction","estimated_compensation_mvar","0.00");
            string isc=GisValue("secondary_fault_estimate","transformer_only_parallel_units_ka","0.00");
            TextNote.Create(doc,view.Id,XY(.4,10.0,0),"INDICADORES CALCULADOS PELO SCRIPT PYTHON | PREMISSAS DOCUMENTADAS",text);
            Box(doc,view,.4,7.05,5.2,9.15,"CARGA NORMAL\nS = "+s+" MVA\nQ = "+q+" MVAr",text,power,0);
            Box(doc,view,6.2,7.05,11.0,9.15,"CORRENTE DE CARGA\n138 kV: "+ip+" kA\n13,8 kV: "+isec+" kA",text,data,0);
            Box(doc,view,12.0,7.05,16.8,9.15,"2 x 25 MVA\nCARGA NORMAL\n"+loading+"%",text,data,0);
            Box(doc,view,.4,4.2,5.2,6.3,"N-1 CRITICA\n"+n1+"% DA PLACA\nDentro da placa",text,data,0);
            Box(doc,view,6.2,4.2,11.0,6.3,"N-1 CARGA TOTAL\n"+n1full+"% DA PLACA\nRejeicao necessaria",text,note,0);
            Box(doc,view,12.0,4.2,16.8,6.3,"FP 0,95 -> 0,98\nQc estimada\n"+comp+" MVAr",text,power,0);
            Box(doc,view,.4,1.35,5.2,3.45,"CURTO 13,8 kV\n1 TR: 13,07 kA\n2 TR: "+isc+" kA",text,note,0);
            Box(doc,view,6.2,1.35,11.0,3.45,"PREMISSA\nZ = 8%\nFonte primaria ideal",text,note,0);
            Box(doc,view,12.0,1.35,16.8,3.45,"LIMITES\nSem rede, motores\nou cabos",text,note,0);
            TextNote.Create(doc,view.Id,XY(.5,.7,0),"Curto de transformador e compensacao sao estimativas; nao selecionar disjuntores por estes valores.",text);
        }

        private static string GisValue(string group,string key,string format)
        {
            string path=Path.Combine(Root,"projetos","19-subestacao-gis","dados","resultados.json");
            using(JsonDocument document=JsonDocument.Parse(File.ReadAllText(path)))
            {
                double value=document.RootElement.GetProperty("results").GetProperty(group).GetProperty(key).GetDouble();
                return value.ToString(format,System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            }
        }

        private static void ExportGIS3D(Document doc,View3D view,string path)
        {
            ImageExportOptions options=new ImageExportOptions();
            options.ExportRange=ExportRange.SetOfViews;
            options.SetViewsAndSheets(new List<ElementId>{view.Id});
            options.FilePath=path;
            options.HLRandWFViewsFileType=ImageFileType.PNG;
            options.ShadowViewsFileType=ImageFileType.PNG;
            options.ZoomType=ZoomFitType.FitToPage;
            options.PixelSize=2400;
            doc.ExportImage(options);
            Log("GIS_3D_IMAGE_EXPORTED "+path);
        }

        private static ViewPlan CreatePlan(Document doc,Level level,string name,double w,double d,int scale){ViewFamilyType t=new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(x=>x.ViewFamily==ViewFamily.FloorPlan);ViewPlan v=ViewPlan.Create(doc,t.Id,level.Id);v.Name=name;v.Scale=scale;v.CropBoxActive=true;v.CropBoxVisible=false;v.CropBox=new BoundingBoxXYZ{Min=new XYZ(M(-2.1),M(-2.1),level.Elevation-M(.1)),Max=new XYZ(M(w+2.1),M(d+2.1),level.Elevation+M(2.9))};foreach(BuiltInCategory c in new[]{BuiltInCategory.OST_Elev,BuiltInCategory.OST_ElevationMarks,BuiltInCategory.OST_Sections,BuiltInCategory.OST_SectionHeads,BuiltInCategory.OST_Callouts})try{v.SetCategoryHidden(new ElementId(c),true);}catch{}return v;}
        private static ViewDrafting CreateDraftingView(Document doc,string name){ViewFamilyType t=new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(x=>x.ViewFamily==ViewFamily.Drafting);ViewDrafting v=ViewDrafting.Create(doc,t.Id);v.Name=name;v.Scale=25;return v;}
        private static View3D Create3DView(Document doc,string name,ProjectSpec spec){ViewFamilyType t=new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(x=>x.ViewFamily==ViewFamily.ThreeDimensional);View3D v=View3D.CreateIsometric(doc,t.Id);v.Name=name;v.DetailLevel=ViewDetailLevel.Fine;v.DisplayStyle=DisplayStyle.Shading;v.Scale=100;v.SetSectionBox(new BoundingBoxXYZ{Min=new XYZ(M(-2.4),M(-2.4),M(-.2)),Max=new XYZ(M(spec.Width+2.4),M(spec.Depth+2.4),M(ModelHeight(spec)+.6))});v.IsSectionBoxActive=true;foreach(BuiltInCategory c in new[]{BuiltInCategory.OST_Levels,BuiltInCategory.OST_Grids})try{v.SetCategoryHidden(new ElementId(c),true);}catch{}try{FillPatternElement solid=new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().First(x=>x.GetFillPattern().IsSolidFill);Color color=PaletteColor(spec.Number);OverrideGraphicSettings walls=new OverrideGraphicSettings();walls.SetSurfaceForegroundPatternId(solid.Id);walls.SetSurfaceForegroundPatternColor(color);walls.SetSurfaceBackgroundPatternId(solid.Id);walls.SetSurfaceBackgroundPatternColor(color);v.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_Walls),walls);OverrideGraphicSettings floors=new OverrideGraphicSettings();Color floor=Lighten(color,.68);floors.SetSurfaceForegroundPatternId(solid.Id);floors.SetSurfaceForegroundPatternColor(floor);floors.SetSurfaceBackgroundPatternId(solid.Id);floors.SetSurfaceBackgroundPatternColor(floor);v.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_Floors),floors);}catch(Exception ex){Log("3D_COLOR_NOTE "+spec.Number+" "+ex.Message);}return v;}
        private static ElementId EnsureTitleBlock(Document doc,Application app,string outDir){string path=Path.Combine(outDir,"familias","Portfolio_A3.rfa");Directory.CreateDirectory(Path.GetDirectoryName(path));if(!File.Exists(path)){if(!File.Exists(TitleBlockTemplate))throw new FileNotFoundException("A3 family template missing",TitleBlockTemplate);using(Document f=app.NewFamilyDocument(TitleBlockTemplate))f.SaveAs(path,new SaveAsOptions{OverwriteExistingFile=true});}FamilySymbol s=new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault(x=>x.Family.Name=="Portfolio_A3");if(s==null){Family loaded;doc.LoadFamily(path,out loaded);s=new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault(x=>x.Family.Name=="Portfolio_A3");}if(s==null)throw new InvalidOperationException("A3 family failed to load");return s.Id;}
        private static ElementId GetTextTypeId(Document doc){ElementId id=doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);if(id!=ElementId.InvalidElementId)return id;return new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().First().Id;}
        private static ElementId GetFooterText(Document doc,ElementId baseId){const string n="Portfolio - 1.8 mm";TextNoteType t=new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault(x=>x.Name==n);if(t!=null)return t.Id;t=(TextNoteType)((TextNoteType)doc.GetElement(baseId)).Duplicate(n);Parameter p=t.get_Parameter(BuiltInParameter.TEXT_SIZE);if(p!=null&&!p.IsReadOnly)p.Set(M(.0018));return t.Id;}
        private static GraphicsStyle GetLineStyle(Document doc,string name,Color color,int weight){Category root=doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);Category c=root.SubCategories.Cast<Category>().FirstOrDefault(x=>x.Name==name);if(c==null)c=doc.Settings.Categories.NewSubcategory(root,name);c.LineColor=color;c.SetLineWeight(weight,GraphicsStyleType.Projection);return c.GetGraphicsStyle(GraphicsStyleType.Projection);}
        private static void DrawLine(Document doc,View view,double x1,double y1,double x2,double y2,GraphicsStyle style,double z){if(Math.Abs(x2-x1)+Math.Abs(y2-y1)<.001)return;DetailCurve c=doc.Create.NewDetailCurve(view,Line.CreateBound(XY(x1,y1,z),XY(x2,y2,z)));if(style!=null)c.LineStyle=style;}
        private static void Circle(Document doc,View view,double x,double y,double r,GraphicsStyle s,double z){for(int i=0;i<12;i++){double a=2*Math.PI*i/12,b=2*Math.PI*(i+1)/12;DrawLine(doc,view,x+r*Math.Cos(a),y+r*Math.Sin(a),x+r*Math.Cos(b),y+r*Math.Sin(b),s,z);}}
        private static void Box(Document doc,View view,double x1,double y1,double x2,double y2,string label,ElementId text,GraphicsStyle s,double z){DrawLine(doc,view,x1,y1,x2,y1,s,z);DrawLine(doc,view,x2,y1,x2,y2,s,z);DrawLine(doc,view,x2,y2,x1,y2,s,z);DrawLine(doc,view,x1,y2,x1,y1,s,z);if(!string.IsNullOrWhiteSpace(label))TextNote.Create(doc,view.Id,XY((x1+x2)/2,(y1+y2)/2,z),label,text);}

        private static void WriteFiles(ProjectSpec s,string outDir,string baseName)
        {
            if (s.Number == 19)
            {
                WriteGISProjectFiles(s, outDir, baseName);
                return;
            }
            string data=Path.Combine(outDir,"dados");Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(outDir,"pranchas","preview"));Directory.CreateDirectory(Path.Combine(outDir,"familias"));
            string csv="Circuito;Zona / sistema;Uso;Status\r\n";int i=0;foreach(string z in s.Systems)csv+="C"+(++i).ToString("00")+";"+z+";Conceitual;Nao dimensionado\r\n";File.WriteAllText(Path.Combine(data,"zonas-e-sistemas.csv"),csv,new UTF8Encoding(true));
            string notes="# Premissas do estudo - "+s.Title+"\n\n## Escopo\nEstudo de portfolio BIM para "+s.Program+"\n\n## Modelo\nArquivo Revit nativo com "+s.Floors+" nivel(is), pisos, "+(s.Enclosed?"paredes externas, divisorias e aberturas":"implantacao, volumes de equipamento e zonas de processo")+". Inclui plantas arquitetonicas, iluminacao, forca/sistemas, diagrama funcional e axonometria.\n\n## Limites\nElementos eletricos sao anotacoes graficas; nao sao familias MEP conectadas nem circuitos calculados pelo Revit. Nao foram realizados calculos de demanda, condutores, protecoes, curto-circuito, queda de tensao, iluminancia, aterramento ou dimensionamento especifico do processo. O estudo nao deve ser usado para obra, compra, aprovacao ou instalacao.\n";
            File.WriteAllText(Path.Combine(data,"premissas-do-estudo.md"),notes,new UTF8Encoding(false));
            string readme="# Projeto "+s.Number.ToString("00")+" — "+s.Title+"\n\nEstudo demonstrativo de modelagem BIM no Revit 2027. "+s.Program+"\n\n## Escopo modelado\n\n- Envelope de estudo: "+s.Width.ToString("0.00")+" × "+s.Depth.ToString("0.00")+" m por nivel; "+s.Floors+" nivel(is).\n- "+s.ModelDescription+"\n- Plantas de arquitetura/implantacao, iluminacao, forca e sistemas, diagrama funcional e axonometria.\n\n## Entregaveis\n\n- [Modelo Revit (.RVT)](revit/"+baseName+".rvt)\n- [Pranchas A3 (.PDF)](pranchas/"+baseName+".pdf)\n- [Zonas e sistemas (.CSV)](dados/zonas-e-sistemas.csv)\n- [Premissas e limites](dados/premissas-do-estudo.md)\n- [Familia de margem A3 (.RFA)](familias/Portfolio_A3.rfa)\n\n## Limites de uso\n\nAs instalacoes sao esquematicas, sem calculo e sem validacao normativa ou executiva. Consulte as premissas antes de interpretar ou reutilizar os dados.\n";
            File.WriteAllText(Path.Combine(outDir,"README.md"),readme,new UTF8Encoding(false));
            string familySource=Path.Combine(Root,"projetos","12-subestacao-e-ccm","familias","Portfolio_A3.rfa");string familyTarget=Path.Combine(outDir,"familias","Portfolio_A3.rfa");if(File.Exists(familySource)&&!File.Exists(familyTarget))File.Copy(familySource,familyTarget);
        }

        private static void WriteGISProjectFiles(ProjectSpec s,string outDir,string baseName)
        {
            string data=Path.Combine(outDir,"dados");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(Path.Combine(outDir,"pranchas","preview"));
            Directory.CreateDirectory(Path.Combine(outDir,"familias"));
            Directory.CreateDirectory(Path.Combine(outDir,"revit"));
            string systems="Sistema;Conteudo;Estado\r\n"+
                "GIS 138 kV;Cinco bays conceituais e barras A/B;geometria nativa Revit sem conectores\r\n"+
                "Transformacao;2 x 25 MVA 138/13,8 kV Z=8% assumida;envelope conceitual com parametros em comentarios\r\n"+
                "Cubiculos 13,8 kV;Sete cubiculos de referencia;sem selecao de fabricante ou capacidade interruptiva\r\n"+
                "Protecao e controle;IEDs, HMI, gateway e comunicacao;diagrama funcional sem ajustes\r\n"+
                "Servicos auxiliares;CA, CC e UPS conceituais;sem potencia ou autonomia dimensionada\r\n"+
                "Canaletas;Envelopes de rota de cabos;sem cabos, ocupacao ou separacao calculados\r\n"+
                "Aterramento;Pendente de dados de solo e estudo especifico;nao dimensionado\r\n";
            File.WriteAllText(Path.Combine(data,"zonas-e-sistemas.csv"),systems,new UTF8Encoding(true));
            string notes="# Premissas do estudo — Subestação GIS\n\n"+
                "Estudo demonstrativo para portfólio. Tensões, carga, fator de potência, potência e impedância dos transformadores são premissas, não dados de rede, fabricante ou campo.\n\n"+
                "## Modelo Revit\n\n"+
                "O layout inclui paredes nativas Revit para as edificações esquemáticas e elementos DirectShape categorizados como Electrical Equipment para bays, cubículos, transformadores e painéis; canaletas e barramentos conceituais ficam em Generic Models. Os DirectShapes não são famílias de fabricante, não têm conectores elétricos e não formam circuitos MEP.\n\n"+
                "## Limites\n\n"+
                "A geometria não verifica isolamento, afastamentos elétricos, estrutura, acesso, ventilação, incêndio, ruído ou requisitos ambientais. Proteções, aterramento, curto completo e coordenação não foram dimensionados. Não usar para obra, compra, aprovação ou instalação.\n";
            File.WriteAllText(Path.Combine(data,"premissas-do-estudo.md"),notes,new UTF8Encoding(false));
            string readme="# Projeto 19 — Subestação de alta tensão GIS\n\n"+
                "Estudo BIM conceitual para uma subestação 138/13,8 kV, com cálculo elétrico preliminar reproduzível.\n\n"+
                "## Escopo e modelo\n\n"+
                "- Implantação de 64 x 42 m.\n- Edifício GIS com separação indicativa para cubículos de 13,8 kV.\n- Cinco bays GIS conceituais, dois transformadores 25 MVA, cubículos MT, casa de controle, painéis de proteção/automação e rotas de canaletas.\n- Paredes do edifício são elementos Wall nativos do Revit. Equipamentos são DirectShape em categorias técnicas com marca e comentários de estudo; não são famílias paramétricas de fabricante e não têm conectores ou circuitos funcionais.\n- O unifilar, a proteção/controle e os diagramas são documentação esquemática em vistas Drafting do Revit.\n\n"+
                "## Cálculos\n\n"+
                "Entradas, fórmulas, resultados e limites estão em [dados](dados/). O script Python implementa carga aparente/reativa, correntes trifásicas, carregamento, contingência N-1, compensação de FP e uma estimativa simplificada de curto no secundário. Não há estudo de curto da rede, seleção de disjuntores, coordenação, cabos, aterramento ou perdas de fabricante.\n\n"+
                "## Arquivos\n\n"+
                "- [Modelo nativo Revit (.RVT)](revit/"+baseName+".rvt)\n- [Pranchas A3 (.PDF)](pranchas/"+baseName+".pdf)\n- [Memorial de cálculo](dados/memorial-de-calculo.md)\n- [Resultados JSON](dados/resultados.json)\n- [Quantitativos extraídos dos DirectShapes](dados/quantitativos-modelo.csv)\n- [Prévias PNG das pranchas](pranchas/preview/)\n- [Vista 3D exportada do Revit](pranchas/preview/19-subestacao-gis-3d.png)\n- [Premissas e limites](dados/premissas-do-estudo.md)\n\n"+
                "## Estado de validação\n\n"+
                "Em geração. O projeto só será marcado como concluído depois de abrir o RVT, conferir categorias/elementos, exportar as oito folhas, renderizar e inspecionar todas as páginas e executar os testes.\n";
            File.WriteAllText(Path.Combine(outDir,"README.md"),readme,new UTF8Encoding(false));
            string familySource=Path.Combine(Root,"projetos","12-subestacao-e-ccm","familias","Portfolio_A3.rfa");
            string familyTarget=Path.Combine(outDir,"familias","Portfolio_A3.rfa");
            if(File.Exists(familySource)&&!File.Exists(familyTarget))File.Copy(familySource,familyTarget);
        }

        private static void WriteGISModelQuantities(Document doc,string outDir)
        {
            List<DirectShape> shapes=new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).Cast<DirectShape>()
                .Where(x=>x.ApplicationId=="PortfolioBIM.GIS").OrderBy(x=>x.Category.Name).ThenBy(x=>x.Id.Value).ToList();
            string path=Path.Combine(outDir,"dados","quantitativos-modelo.csv");
            using(StreamWriter writer=new StreamWriter(path,false,new UTF8Encoding(true)))
            {
                writer.WriteLine("element_id;categoria_revit;marca;comentarios");
                foreach(DirectShape shape in shapes)
                {
                    string mark=shape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()??"";
                    string comments=shape.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()??"";
                    writer.WriteLine(shape.Id.Value+";"+shape.Category.Name+";"+mark+";"+comments.Replace(';',',').Replace('\r',' ').Replace('\n',' '));
                }
            }
            long electrical=shapes.Count(x=>x.Category.Id==new ElementId(BuiltInCategory.OST_ElectricalEquipment));
            Log("GIS_MODEL_QUANTITIES direct_shapes="+shapes.Count+" electrical_equipment="+electrical+" csv="+path);
        }

        private static void Log(string msg){try{Directory.CreateDirectory(Path.GetDirectoryName(BatchLog));File.AppendAllText(BatchLog,msg+Environment.NewLine,new UTF8Encoding(false));}catch{}}
        private static XYZ XY(double x,double y,double z)=>new XYZ(M(x),M(y),z);
        private static double M(double x)=>UnitUtils.ConvertToInternalUnits(x,UnitTypeId.Meters);

        private sealed class ProjectSpec
        {
            public int Number;public string Slug="",Title="",Program="",UpperProgram="",LightNote="",PowerNote="";public double Width,Depth;public int Floors;public bool Enclosed;public string[] Systems=Array.Empty<string>();public Equipment[] Equipment=Array.Empty<Equipment>();public readonly List<Segment> Partitions=new List<Segment>();public readonly List<Space> Spaces=new List<Space>();
            public string ModelDescription
            {
                get
                {
                    if(Enclosed)return "Pisos, paredes perimetrais, esquadrias conceituais e "+RoofDescription(Number)+".";
                    if(Number==19)return "Subestacao GIS com paredes Revit, bays e cubiculos Electrical Equipment, transformadores, controles, canaletas e vista 3D; geometria conceitual sem conectores MEP.";
                    if(Number==11||Number==14)return "Implantacao FV com mesas inclinadas, conversao e abrigos tecnicos localizados.";
                    if(Number==13)return "Implantacao de processo com tanques cilindricos, bombeamento e casa de bombas coberta.";
                    if(Number==10)return "Implantacao de vagas, carregadores e coberturas tipo canopy sobre zonas de recarga.";
                    if(Number==12)return "Patio aberto de subestacao, transformacao, CCM e coberturas tecnicas localizadas.";
                    return "Superficie de estudo, equipamentos e zonas funcionais especificas do processo.";
                }
            }
        }
        private sealed class Segment{public int Floor;public double X1,Y1,X2,Y2;public Segment(int f,double x1,double y1,double x2,double y2){Floor=f;X1=x1;Y1=y1;X2=x2;Y2=y2;}}
        private sealed class Space{public int Floor;public string Name;public double X,Y;public Space(int f,string n,double x,double y){Floor=f;Name=n;X=x;Y=y;}}
        private sealed class Equipment{public string Name;public double X,Y,W,D,H;public int Floor;public Equipment(string n,double x,double y,double w,double d,double h,int f){Name=n;X=x;Y=y;W=w;D=d;H=h;Floor=f;}}
        private sealed class PortfolioFailureAudit : IFailuresPreprocessor
        {
            private readonly string _number;
            public PortfolioFailureAudit(string number){_number=number;}
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                IList<FailureMessageAccessor> messages=accessor.GetFailureMessages();
                if(messages.Count==0)return FailureProcessingResult.Continue;
                foreach(FailureMessageAccessor message in messages)
                {
                    string failing=string.Join(",",message.GetFailingElementIds().Select(x=>x.Value.ToString()).ToArray());
                    List<string> details=new List<string>();
                    foreach(ElementId id in message.GetFailingElementIds()){string detail;details.Add(_wallAuditDescriptions.TryGetValue(id.Value,out detail)?detail:"id="+id.Value);}
                    Log("REVIT_FAILURE " + _number + " " + message.GetSeverity() + " ids=" + failing + " elements="+string.Join(" | ",details)+" " + message.GetDescriptionText());
                }
                return FailureProcessingResult.ProceedWithRollBack;
            }
        }
    }
}
