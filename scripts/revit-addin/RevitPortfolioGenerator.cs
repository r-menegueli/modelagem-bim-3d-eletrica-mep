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
        private static readonly string Root = ResolveRepositoryRoot();
        private const string TemplatePath = @"C:\ProgramData\Autodesk\RVT 2027\Templates\Portuguese\DefaultBRAPTB.rte";
        private const string TitleBlockTemplate = @"C:\ProgramData\Autodesk\RVT 2027\Family Templates\Portuguese\Blocos de margens e carimbo\A3 métrico.rft";
        private static readonly string BatchLog = Path.Combine(Root, "scripts", "revit-addin", "revit-batch.log");
        private static readonly string BatchRange = Path.Combine(Root, "scripts", "revit-addin", "batch-range.txt");
        private static bool _started;
        private static string _wallAuditProject = "";
        private static readonly Dictionary<long,string> _wallAuditDescriptions = new Dictionary<long,string>();

        private static string ResolveRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(Path.GetDirectoryName(typeof(RevitPortfolioGenerator).Assembly.Location));
            while (current != null)
            {
                string projectDirectory = Path.Combine(current.FullName, "projetos", "19-subestacao-gis");
                string controlFile = Path.Combine(current.FullName, "scripts", "revit-addin", "batch-range.txt");
                if (Directory.Exists(projectDirectory) && File.Exists(controlFile)) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Nao foi possivel localizar a raiz do repositorio com Projeto 19 e batch-range.txt a partir de " + typeof(RevitPortfolioGenerator).Assembly.Location);
        }

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
            if (!File.Exists(BatchRange))
            {
                _started = true;
                uiApp.Idling -= OnIdling;
                Log("BATCH_DISABLED missing range file");
                return;
            }
            string rangeText = File.ReadAllText(BatchRange).Trim();
            if (rangeText.Length == 0 || rangeText.Equals("DISABLED", StringComparison.OrdinalIgnoreCase) || rangeText.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                _started = true;
                uiApp.Idling -= OnIdling;
                Log("BATCH_DISABLED explicit range=" + (rangeText.Length == 0 ? "<empty>" : rangeText));
                return;
            }
            var selectedProjects = new HashSet<int>();
            try
            {
                foreach (string entry in rangeText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] bounds = entry.Trim().Split('-');
                    int first = int.Parse(bounds[0]);
                    int last = bounds.Length > 1 ? int.Parse(bounds[1]) : first;
                    for (int number = Math.Min(first, last); number <= Math.Max(first, last); number++)
                        if (number >= 1 && number <= 34) selectedProjects.Add(number);
                }
            }
            catch (Exception ex)
            {
                _started = true;
                uiApp.Idling -= OnIdling;
                Log("BATCH_DISABLED invalid range=" + rangeText + " error=" + ex.Message);
                return;
            }
            if (selectedProjects.Count == 0)
            {
                _started = true;
                uiApp.Idling -= OnIdling;
                Log("BATCH_DISABLED range resolved empty: " + rangeText);
                return;
            }
            _started = true;
            uiApp.Idling -= OnIdling;
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
            string modelPath = number=="19"?Path.Combine(outDir,"revit","reconstrucao-r9","19-subestacao-gis-r9.rvt"):Path.Combine(outDir,"revit",baseName+".rvt");
            string pdfPath = number=="19"?Path.Combine(outDir,"pranchas","reconstrucao-r9","19-subestacao-gis-r9.pdf"):Path.Combine(outDir,"pranchas",baseName+".pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath));
            if (s.Number == 19)
            {
                BuildGISProject(uiApp, s, outDir, baseName, modelPath, pdfPath);
                return;
            }
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
            ViewSheet s=CreateSheetBase(doc,block,number,name,text,notes,spec);
            Viewport p=Viewport.Create(doc,s.Id,view.Id,new XYZ(M(.135),M(.15),0));ElementType pt=doc.GetElement(p.GetTypeId()) as ElementType;Parameter show=pt==null?null:pt.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);if(show!=null&&!show.IsReadOnly)show.Set(0);
            if(spec.Number==19&&view is ViewDrafting)
            {
                // TextNote glyphs keep paper size; fitting them by repeatedly scaling the drafting
                // view collapses the diagram while the labels remain full-size.
                view.Scale=view.Name.StartsWith("Q-01",StringComparison.OrdinalIgnoreCase)?90:
                    view.Name.StartsWith("M-01",StringComparison.OrdinalIgnoreCase)?80:75;
                doc.Regenerate();
                p.SetBoxCenter(new XYZ(M(.205),M(.150),0));
                doc.Regenerate();
                Outline box=p.GetBoxOutline();
                double width=box.MaximumPoint.X-box.MinimumPoint.X,height=box.MaximumPoint.Y-box.MinimumPoint.Y;
                if(box.MinimumPoint.X<M(.015)||box.MaximumPoint.X>M(.400)||box.MinimumPoint.Y<M(.065)||box.MaximumPoint.Y>M(.236))
                    Log("GIS_DRAFTING_VIEWPORT_WARNING "+view.Name+" width_m="+UnitUtils.ConvertFromInternalUnits(width,UnitTypeId.Meters).ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" height_m="+UnitUtils.ConvertFromInternalUnits(height,UnitTypeId.Meters).ToString("0.000",System.Globalization.CultureInfo.InvariantCulture));
            }
            else FitViewportToSheet(doc,p,view);
            return s.Id;
        }

        private static ViewSheet CreateSheetBase(Document doc,ElementId block,string number,string name,ElementId text,string notes,ProjectSpec spec)
        {
            ViewSheet s=ViewSheet.Create(doc,block);s.SheetNumber=number;s.Name=name;
            GraphicsStyle border=GetLineStyle(doc,"BIM-ANOTACOES",new Color(53,59,66),2);
            DrawLine(doc,s,.010,.010,.410,.010,border,0);DrawLine(doc,s,.410,.010,.410,.287,border,0);DrawLine(doc,s,.410,.287,.010,.287,border,0);DrawLine(doc,s,.010,.287,.010,.010,border,0);DrawLine(doc,s,.010,.060,.410,.060,border,0);DrawLine(doc,s,.225,.010,.225,.060,border,0);DrawLine(doc,s,.320,.010,.320,.060,border,0);DrawLine(doc,s,.390,.010,.390,.060,border,0);
            ElementId small=GetFooterText(doc,text);TextNote.Create(doc,s.Id,new XYZ(M(.255),M(.246),0),M(.143),notes,text);
            TextNote.Create(doc,s.Id,new XYZ(M(.018),M(.043),0),"PORTFOLIO BIM | " + spec.Title.ToUpperInvariant(),small);TextNote.Create(doc,s.Id,new XYZ(M(.018),M(.022),0),"Estudo demonstrativo - Revit 2027 - nao executivo",small);
            TextNote.Create(doc,s.Id,new XYZ(M(.232),M(.043),0),"PRJ-BIM-"+spec.Number.ToString("00")+" | "+spec.Title.ToUpperInvariant(),small);TextNote.Create(doc,s.Id,new XYZ(M(.232),M(.022),0),"ELABORADO POR: Rogério Eduardo Moreira Menegueli",small);
            TextNote.Create(doc,s.Id,new XYZ(M(.327),M(.043),0),number,small);TextNote.Create(doc,s.Id,new XYZ(M(.327),M(.022),0),name.ToUpperInvariant(),small);TextNote.Create(doc,s.Id,new XYZ(M(.395),M(.032),0),"A3",small);return s;
        }

        private static ElementId CreateGISShowcaseSheet(Document doc,ElementId block,string number,string name,IList<View3D> views,ElementId text,string notes,ProjectSpec spec)
        {
            if(views==null||views.Count!=4)throw new ArgumentException("A folha BIM de vistas 3D exige exatamente quatro vistas.",nameof(views));
            ViewSheet sheet=CreateSheetBase(doc,block,number,name,text,notes,spec);
            double[] centersX={.070,.180,.070,.180};
            double[] centersY={.231,.231,.112,.112};
            for(int i=0;i<views.Count;i++)
            {
                Viewport viewport=Viewport.Create(doc,sheet.Id,views[i].Id,new XYZ(M(centersX[i]),M(centersY[i]),0));
                ElementType type=doc.GetElement(viewport.GetTypeId()) as ElementType;
                Parameter show=type==null?null:type.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                if(show!=null&&!show.IsReadOnly)show.Set(0);
                FitViewportToRect(doc,viewport,views[i],.102,.102,centersX[i],centersY[i]);
            }
            return sheet.Id;
        }

        private static ElementId CreateGISPerspectiveSheet(Document doc,ElementId block,string number,string name,View3D view,ElementId text,string notes,ProjectSpec spec)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            ViewSheet sheet=CreateSheetBase(doc,block,number,name,text,notes,spec);
            Viewport viewport=Viewport.Create(doc,sheet.Id,view.Id,new XYZ(M(.205),M(.145),0));
            ElementType type=doc.GetElement(viewport.GetTypeId()) as ElementType;
            Parameter show=type==null?null:type.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
            if(show!=null&&!show.IsReadOnly)show.Set(0);
            FitViewportToRect(doc,viewport,view,.386,.168,.205,.145);
            return sheet.Id;
        }

        private static void FitViewportToRect(Document doc,Viewport viewport,View view,double maxWidth,double maxHeight,double centerX,double centerY)
        {
            doc.Regenerate();
            for(int i=0;i<12;i++)
            {
                Outline box=viewport.GetBoxOutline();
                double width=box.MaximumPoint.X-box.MinimumPoint.X,height=box.MaximumPoint.Y-box.MinimumPoint.Y;
                double factor=Math.Max(width/M(maxWidth),height/M(maxHeight));
                if(factor<=1.001)break;
                int next=(int)Math.Ceiling(view.Scale*factor*1.02);
                if(next<=view.Scale)next=view.Scale+1;
                view.Scale=next;doc.Regenerate();
            }
            viewport.SetBoxCenter(new XYZ(M(centerX),M(centerY),0));
            doc.Regenerate();
            Outline fitted=viewport.GetBoxOutline();
            if(fitted.MaximumPoint.X-fitted.MinimumPoint.X>M(maxWidth)*1.005||fitted.MaximumPoint.Y-fitted.MinimumPoint.Y>M(maxHeight)*1.005)
                Log("GIS_VIEWPORT_FIT_WARNING "+view.Name+" scale="+view.Scale);
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
        private static void BuildGISProject(UIApplication uiApp,ProjectSpec spec,string outDir,string baseName,string modelPath,string pdfPath)
        {
            EnsureGISFamilyFiles(uiApp.Application,outDir);
            Log("GIS_FAMILY_LIBRARY_READY " + Path.Combine(outDir,"familias","reconstrucao-r9"));
            ProjectSpec layout=new ProjectSpec{Number=19,Slug=spec.Slug,Title=spec.Title,Width=72,Depth=56,Floors=1,Enclosed=false,Program=spec.Program,UpperProgram=spec.UpperProgram,LightNote=spec.LightNote,PowerNote=spec.PowerNote,Systems=spec.Systems};
            Document doc=uiApp.Application.NewProjectDocument(TemplatePath);
            var sheetIds=new List<ElementId>();
            List<ElementId> roofIds=new List<ElementId>();
            try
            {
            using(Transaction tx=new Transaction(doc,"Reconstrucao BIM R9 - GIS 138/13,8 kV"))
                {
                    tx.Start();
                    FailureHandlingOptions failureOptions=tx.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new PortfolioFailureAudit("19"));
                    failureOptions.SetClearAfterRollback(true);
                    tx.SetFailureHandlingOptions(failureOptions);
                    doc.ProjectInformation.Name="Subestacao GIS 138-13,8 kV - Estudo de engenharia BIM";
                    doc.ProjectInformation.Number="PRJ-BIM-19-R9";
                    doc.ProjectInformation.ClientName="Estudo demonstrativo";
                    Level level=FindOrCreateLevel(doc,0,"00 - Implantacao - ±0,00 m");
                    WallType sourceWall=new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First(x=>x.Kind==WallKind.Basic);
                    FloorType sourceFloor=new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();
                    Material wallMat=GetOrCreateMaterial(doc,"GIS-R9-Concreto",new Color(151,157,160));
                    Material floorMat=GetOrCreateMaterial(doc,"GIS-R9-Piso",new Color(205,207,202));
                    Material metalMat=GetOrCreateMaterial(doc,"GIS-R9-Metal",new Color(75,86,93));
                    WallType wallType=MaterializeHostType(doc,sourceWall,"GIS-R9 - Alvenaria tecnica",wallMat);
                    FloorType floorType=MaterializeHostType(doc,sourceFloor,"GIS-R9 - Piso industrial",floorMat);
                    Dictionary<string,FamilySymbol> symbols=LoadGISFamilySymbols(doc,outDir);

                    // Vias e pisos são elementos nativos; as edificações usam paredes, pisos e lajes nativos do Revit.
                    CreateGISFloor(doc,floorType,level,4,4,68,8,0);
                    CreateGISFloor(doc,floorType,level,5,28,39,30.5,0);
                    CreateGISFloor(doc,floorType,level,41,28,67,30.5,0);
                    CreateGISFloor(doc,floorType,level,39,8,41,48,0);
                    CreateGISFloor(doc,floorType,level,7,9,38,27,0);
                    CreateGISFloor(doc,floorType,level,42,9,65,27,0);
                    CreateGISFloor(doc,floorType,level,7,34,29,49,0);
                    CreateGISBuilding(doc,wallType,level,7,9,38,27,6.4,"GIS Hall 145 kV",true);
                    CreateGISBuilding(doc,wallType,level,42,9,65,27,5.4,"Edificio cubiculos 13,8 kV",false);
                    CreateGISBuilding(doc,wallType,level,7,34,29,49,4.2,"Casa de controle e protecao",false);
                    roofIds=new List<ElementId>
                    {
                        AddGISFamilyRoof(doc,symbols,"BuildingRoof_GIS","COB-GIS-01","Cobertura metalica estimada; estrutura, cargas e ventilacao pendentes.",22.5,18.0,6.28).Id,
                        AddGISFamilyRoof(doc,symbols,"BuildingRoof_MV","COB-MT-01","Cobertura metalica estimada; estrutura, cargas e ventilacao pendentes.",53.5,18.0,5.28).Id,
                        AddGISFamilyRoof(doc,symbols,"BuildingRoof_Control","COB-CTRL-01","Cobertura metalica estimada; estrutura, cargas e ventilacao pendentes.",18.0,41.5,4.08).Id
                    };
                    AddWall(doc,wallType,level,18,34,18,49,M(4.2));
                    AddWall(doc,wallType,level,18,41.5,29,41.5,M(4.2));
                    AddWall(doc,wallType,level,48.5,9,48.5,27,M(5.4));
                    AddWall(doc,wallType,level,41,31,41,40,M(5.0)); // barreira corta-fogo conceitual entre áreas de transformadores
                    AddGISFence(doc,3,69,3,53);

                    // GIS: duas barras principais, cinco bays (2 linhas, 2 transformadores e acoplamento).
                    string[] bayTags={"GIS-L01","GIS-T01","GIS-BC01","GIS-T02","GIS-L02"};
                    string[] bayNames={"Entrada de linha 01","Transformador TR-01","Acoplamento de barras","Transformador TR-02","Entrada de linha 02"};
                    // Five 800 mm bays are laid out at the catalog reference pitch; vendor GA remains pending.
                    double[] bayX={18.0,18.8,19.6,20.4,21.2};
                    for(int i=0;i<bayTags.Length;i++)
                    {
                        string bay=bayTags[i];double x=bayX[i];
                        AddGISFamilyInstance(doc,symbols,"GIS_BayFrame",bay+"-ENV",bayNames[i]+"; módulo 800 mm e envelope 5,7 m de referência combinados como aproximação; GA do pedido pendente.",x,18.8,.05);
                        if(i==2)
                            AddGISFamilyInstance(doc,symbols,"GIS_CircuitBreaker",bay+"-52","Disjuntor do acoplador entre barras A/B; rating de interrupção depende do estudo da fonte.",x,19.35,.25);
                        else
                            AddGISFamilyInstance(doc,symbols,"GIS_CircuitBreaker",bay+"-52","Disjuntor de bay classe 145 kV; rating de interrupção depende do estudo da fonte.",x,15.55,.25);
                        AddGISFamilyInstance(doc,symbols,"GIS_Disconnector",bay+"-89A","Seccionadora seletora da barra A; estado operacional documentado no unifilar.",x,18.0,.75);
                        AddGISFamilyInstance(doc,symbols,"GIS_Disconnector",bay+"-89B","Seccionadora seletora da barra B; estado operacional documentado no unifilar.",x,20.8,.75);
                        AddGISFamilyInstance(doc,symbols,"GIS_EarthingSwitch",bay+"-89G","Chave de aterramento esquemática; intertravamentos e rating pendentes.",x-.2,i==2?19.35:15.55,.35);
                        if(i!=2) AddGISFamilyInstance(doc,symbols,"GIS_CurrentTransformer",bay+"-CT","TC GIS de estudo; relação, classe, núcleos e burden pendentes.",x,14.25,.45);
                        if(i==0||i==4) AddGISFamilyInstance(doc,symbols,"GIS_VoltageTransformer",bay+"-VT","TP GIS de estudo; relação e classe pendentes de concessionária e estudo.",x,13.15,.35);
                        if(i==0||i==4) AddGISFamilyInstance(doc,symbols,"GIS_Termination",bay+"-TERM","Interface de linha GIS; conexão aérea/cabo e terminal final pendentes.",x,12.0,.25);
                    }
                    foreach(double x in bayX)
                    {
                        AddGISFamilyInstance(doc,symbols,"GIS_BusModule","GIS-BUS-A-MOD-"+x.ToString("00.0"),"Carcaça externa de barra encapsulada A, módulo geométrico de 800 mm; sem condutor interno ou rating representado.",x,18.0,2.55);
                        AddGISFamilyInstance(doc,symbols,"GIS_BusModule","GIS-BUS-B-MOD-"+x.ToString("00.0"),"Carcaça externa de barra encapsulada B, módulo geométrico de 800 mm; sem condutor interno ou rating representado.",x,20.8,2.55);
                    }
                    AddGISPrimaryRoute(doc,"GIS-BUSA-ENCLOSURE","Continuous outer GIS bus enclosure A; internal conductor and switching contacts are not modeled.",new XYZ(M(17.6),M(18.0),M(2.74)),new XYZ(M(21.6),M(18.0),M(2.74)),.17,metalMat);
                    AddGISPrimaryRoute(doc,"GIS-BUSB-ENCLOSURE","Continuous outer GIS bus enclosure B; internal conductor and switching contacts are not modeled.",new XYZ(M(17.6),M(20.8),M(2.74)),new XYZ(M(21.6),M(20.8),M(2.74)),.17,metalMat);
                    for(int i=0;i<bayX.Length;i++)
                    {
                        double x=bayX[i];string bay=bayTags[i];
                        AddGISPrimaryRoute(doc,bay+"-89A-STUB","Geometric casing route from bus A module to selector disconnector; not a Revit electrical connection.",new XYZ(M(x),M(18.0),M(2.74)),new XYZ(M(x),M(18.0),M(2.15)),.10,metalMat);
                        AddGISPrimaryRoute(doc,bay+"-89B-STUB","Geometric casing route from bus B module to selector disconnector; not a Revit electrical connection.",new XYZ(M(x),M(20.8),M(2.74)),new XYZ(M(x),M(20.8),M(2.15)),.10,metalMat);
                    }
                    for(int phase=0;phase<3;phase++)
                    {
                        double ctY=14.25-.40+.80*(.22+phase*.28);
                        double tr01HVx=32.0-1.45+phase*1.45;
                        double tr02HVx=51.5-1.45+phase*1.45;
                        double tr01MVx=32.0-1.05+phase*1.05;
                        double tr02MVx=51.5-1.05+phase*1.05;
                        double tr01CabinetX=44.0-.24+phase*.24;
                        double tr02CabinetX=60.28-.24+phase*.24;
                        double tr01ApproachX=43.10+phase*.24;
                        double tr02ApproachX=61.20-phase*.24;
                        string phaseName=phase==0?"A":(phase==1?"B":"C");
                        AddGISPrimaryRoutePolyline(doc,"GIS-TR01-HV-PH-"+phaseName,"Approximate geometric 138 kV phase route from GIS-T01 CT terminal to TR-01 HV bushing; not an electrical connector or selected busduct.",new[]{
                            new XYZ(M(19.064),M(ctY),M(1.178)),new XYZ(M(19.064),M(ctY),M(2.55)),new XYZ(M(19.064),M(29.8),M(2.55)),new XYZ(M(tr01HVx),M(29.8),M(2.55)),new XYZ(M(tr01HVx),M(29.8),M(5.55)),new XYZ(M(tr01HVx),M(35.9),M(5.55))},.075,metalMat);
                        AddGISPrimaryRoutePolyline(doc,"GIS-TR02-HV-PH-"+phaseName,"Approximate geometric 138 kV phase route from GIS-T02 CT terminal to TR-02 HV bushing; not an electrical connector or selected busduct.",new[]{
                            new XYZ(M(20.664),M(ctY),M(1.178)),new XYZ(M(20.664),M(ctY),M(2.55)),new XYZ(M(20.664),M(29.3),M(2.55)),new XYZ(M(tr02HVx),M(29.3),M(2.55)),new XYZ(M(tr02HVx),M(29.3),M(5.55)),new XYZ(M(tr02HVx),M(35.9),M(5.55))},.075,metalMat);
                        AddGISPrimaryRoutePolyline(doc,"TR01-MV-PH-"+phaseName,"Approximate 13.8 kV phase route envelope from TR-01 bushing to QMT-A-IN rear entry; conductor count, cable section, ampacity and installation remain unselected.",new[]{
                            new XYZ(M(tr01MVx),M(33.7),M(4.48)),new XYZ(M(tr01MVx),M(31.5),M(4.48)),new XYZ(M(tr01MVx),M(31.5),M(.55)),new XYZ(M(tr01ApproachX),M(31.5),M(.55)),new XYZ(M(tr01ApproachX),M(25.8),M(.55)),new XYZ(M(tr01ApproachX),M(15.0),M(.55)),new XYZ(M(tr01CabinetX),M(15.0),M(.55)),new XYZ(M(tr01CabinetX),M(14.795),M(.55))},.04,metalMat);
                        AddGISPrimaryRoutePolyline(doc,"TR02-MV-PH-"+phaseName,"Approximate 13.8 kV phase route envelope from TR-02 bushing to QMT-B-IN rear entry; conductor count, cable section, ampacity and installation remain unselected.",new[]{
                            new XYZ(M(tr02MVx),M(33.7),M(4.48)),new XYZ(M(tr02MVx),M(31.5),M(4.48)),new XYZ(M(tr02MVx),M(31.5),M(.55)),new XYZ(M(tr02ApproachX),M(31.5),M(.55)),new XYZ(M(tr02ApproachX),M(25.8),M(.55)),new XYZ(M(tr02ApproachX),M(15.0),M(.55)),new XYZ(M(tr02CabinetX),M(15.0),M(.55)),new XYZ(M(tr02CabinetX),M(14.795),M(.55))},.04,metalMat);
                    }

                    // Transformadores em duas áreas independentes, com acessórios BIM separados e marcas rastreáveis.
                    AddGISTransformer(doc,symbols,"TR-01",32.0,34.8);
                    AddGISTransformer(doc,symbols,"TR-02",51.5,34.8);

                    // Cubículos em duas seções de sete unidades, mais acoplamento normalmente aberto.
                    string[] aTags={"A-IN","A-MED","A-F01","A-F02","A-F03","A-F04","A-F05"};
                    string[] bTags={"B-F01","B-F02","B-F03","B-F04","B-F05","B-MED","B-IN"};
                    for(int i=0;i<aTags.Length;i++)
                    {
                        AddGISFamilyInstance(doc,symbols,"MV_Cubicle","QMT-"+aTags[i],"Cubiculo 13,8 kV; classe 17,5 kV de estudo; rating e curto pendentes.",44.0+i*.88,14.1,.05);
                        AddGISFamilyInstance(doc,symbols,"MV_Cubicle","QMT-"+bTags[i],"Cubiculo 13,8 kV; classe 17,5 kV de estudo; rating e curto pendentes.",55.0+i*.88,14.1,.05);
                    }
                    AddGISFamilyInstance(doc,symbols,"MV_BusCoupler","QMT-TIE","Acoplamento 13,8 kV; normalmente aberto por premissa, intertravamento a estudar.",52.0,14.1,.05);
                    foreach(double x in new[]{46.6,57.6})
                        AddGISFamilyInstance(doc,symbols,"MVBusModule",x<50?"BUS-MV-A":"BUS-MV-B","Barra 13,8 kV; rating térmico e curto pendentes.",x,16.15,2.55);

                    // Painéis de proteção, telecomunicações, CA/CC, UPS e baterias na casa de controle.
                    string[] pTags={"P-L01","P-L02","P-BUS","P-TR21-A","P-TR21-B","P-TR22-A","P-TR22-B","P-BC","P-SCADA"};
                    for(int i=0;i<pTags.Length;i++)
                    {
                        double x=9.8+(i%5)*2.0,y=i<5?37.0:39.4;
                        AddGISFamilyInstance(doc,symbols,"ProtectionPanel",pTags[i],"Painel funcional; IEDs, relações TC/TP, ajustes, protocolo e lógica ainda não especificados.",x,y,.05);
                    }
                    FamilyInstance qac=AddGISFamilyInstance(doc,symbols,"AuxiliaryPanelAC","QAC-01","Quadro auxiliar CA; 380 V é premissa de estudo, cargas e seletividade pendentes.",10.0,44.0,.05);
                    AddGISFamilyInstance(doc,symbols,"AuxiliaryPanelDC","QCC-01","Quadro auxiliar CC; 125 Vcc é premissa de estudo, cargas e autonomia pendentes.",12.3,44.0,.05);
                    FamilyInstance ups=AddGISFamilyInstance(doc,symbols,"UPS_Cabinet","UPS-01","UPS conceitual; potência, bypass e autonomia não dimensionados.",15.0,44.0,.05);
                    AddGISFamilyInstance(doc,symbols,"BatteryRack","BAT-01","Banco de baterias conceitual; tensão, Ah e missão pendentes.",17.8,44.0,.05);
                    TryCreateGISAuxCircuit(doc,qac,ups);

                    // Canaletas e leitos segmentados conectam GIS, transformadores e edificações; ocupação e cabos seguem pendentes.
                    AddGISCableRoute(doc,symbols,"CT-GIS-MV",10.0,25.8,54.0,25.8,.02);
                    AddGISCableRoute(doc,symbols,"CT-TR21",32.0,34.2,32.0,25.8,.02);
                    AddGISCableRoute(doc,symbols,"CT-TR22",51.5,34.2,51.5,25.8,.02);
                    AddGISCableRoute(doc,symbols,"CT-GIS-CTRL",12.0,27.3,12.0,34.0,.02);
                    AddGISCableTrayPath(doc,symbols,"LEITO-CTRL",new[]{new XYZ(M(9.5),M(35.2),M(4.15)),new XYZ(M(26.5),M(35.2),M(4.15))});
                    AddGISCableTrayPath(doc,symbols,"LEITO-GIS",new[]{new XYZ(M(9.2),M(10.0),M(4.4)),new XYZ(M(36.5),M(10.0),M(4.4))});
                    AddGISCableTrayPath(doc,symbols,"LEITO-MV-A",new[]{new XYZ(M(32.0),M(25.8),M(.55)),new XYZ(M(43.1),M(25.8),M(.55)),new XYZ(M(43.1),M(14.795),M(.55)),new XYZ(M(44.0),M(14.795),M(.55))});
                    AddGISCableTrayPath(doc,symbols,"LEITO-MV-B",new[]{new XYZ(M(51.5),M(25.8),M(.55)),new XYZ(M(61.2),M(25.8),M(.55)),new XYZ(M(61.2),M(14.795),M(.55)),new XYZ(M(60.28),M(14.795),M(.55))});

                    // Malha conceitual de aterramento, elementos nativos ModelCurve, sem dimensionamento de toque/passo.
                    AddGISGroundGrid(doc,4,68,4,52,4.0);
                    doc.Regenerate();
                    if(tx.Commit()!=TransactionStatus.Committed) throw new InvalidOperationException("A transação BIM GIS não foi confirmada pelo Revit.");
                }
                CreateGISSheets(uiApp.Application,doc,layout,outDir,baseName,sheetIds,roofIds);
                doc.SaveAs(modelPath,new SaveAsOptions{OverwriteExistingFile=true});
                doc.Save();
                Log("MODEL_SAVED 19 " + modelPath + " bytes=" + new FileInfo(modelPath).Length);
                WriteGISModelQuantities(doc,outDir);
                try
                {
                    PDFExportOptions options=new PDFExportOptions{Combine=true,FileName=Path.GetFileNameWithoutExtension(pdfPath)};
                    bool ok=doc.Export(Path.GetDirectoryName(pdfPath),sheetIds,options);
                    Log("PDF_EXPORTED 19 " + ok + " sheets=" + sheetIds.Count + " path=" + pdfPath);
                }
                catch(Exception ex){Log("PDF_FAILED 19 " + ex);throw;}
                string previewDir=Path.Combine(outDir,"pranchas","reconstrucao-r9","preview");Directory.CreateDirectory(previewDir);
                List<View3D> views3D=new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().Where(v=>v.Name.StartsWith("3D-")).OrderBy(v=>v.Name).ToList();
                foreach(View3D view in views3D)
                {
                    string code=view.Name.Substring(0,5);
                try{ExportGIS3D(doc,view,Path.Combine(previewDir,"19-subestacao-gis-r9-"+code+".png"));}
                    catch(Exception ex){Log("GIS_3D_EXPORT_FAILED "+code+" "+ex);}
                }
                Log("GIS_RECONSTRUCTION_GENERATED families="+GetGISFamilySpecs().Count+" sheets="+sheetIds.Count+" views3d="+views3D.Count);
            }
            finally{try{if(doc.IsValidObject)doc.Close(false);}catch(Exception ex){Log("GIS_DOC_CLOSE_NOTE "+ex.Message);}}
        }

        private static ElementId CreateGISFloor(Document doc,FloorType floorType,Level level,double x1,double y1,double x2,double y2,double z)
        {
            return Floor.Create(doc,new List<CurveLoop>{RectangleLoop(x1,y1,x2,y2,M(z))},floorType.Id,level.Id).Id;
        }

        private static FamilyInstance AddGISFamilyRoof(Document doc,Dictionary<string,FamilySymbol> symbols,string key,string mark,string comments,double x,double y,double z)
        {
            return AddGISFamilyInstance(doc,symbols,key,mark,comments,x,y,z);
        }

        private static void CreateGISBuilding(Document doc,WallType wallType,Level level,double x1,double y1,double x2,double y2,double height,string label,bool broadDoor)
        {
            double h=M(height);
            AddWall(doc,wallType,level,x1,y1,x1,y2,h);
            AddWall(doc,wallType,level,x2,y1,x2,y2,h);
            AddWall(doc,wallType,level,x1,y2,x2,y2,h);
            if(broadDoor)
            {
                AddWall(doc,wallType,level,x1,y1,17.0,y1,h);
                AddWall(doc,wallType,level,23.0,y1,x2,y1,h);
            }
            else
            {
                AddWall(doc,wallType,level,x1,y1,x1+4.0,y1,h);
                AddWall(doc,wallType,level,x1+5.2,y1,x2-4.0,y1,h);
                AddWall(doc,wallType,level,x2-2.8,y1,x2,y1,h);
            }
            Log("GIS_BUILDING_CREATED " + label + " " + (x2-x1).ToString("0.0") + "x" + (y2-y1).ToString("0.0") + "m height="+height.ToString("0.0"));
        }

        private static void AddGISTransformer(Document doc,Dictionary<string,FamilySymbol> symbols,string tag,double x,double y)
        {
            AddGISFamilyInstance(doc,symbols,"Transformer_Base",tag+"-BASE","Base de concreto conceitual; cálculo estrutural e geotecnia pendentes.",x,y,.02);
            AddGISFamilyInstance(doc,symbols,"OilContainment",tag+"-CONT","Contenção conceitual; volume de óleo, drenagem e requisito ambiental pendentes.",x,y,.28);
            AddGISFamilyInstance(doc,symbols,"Transformer_Tank",tag,"40 MVA, 138/13,8 kV; geometria estimada, Z=10% provisória, fabricante e placa não selecionados.",x,y,.58);
            for(int i=0;i<6;i++)
            {
                AddGISFamilyInstance(doc,symbols,"Transformer_Radiator",tag+"-RAD-"+(i+1).ToString("00"),"Radiador estimado; área térmica e método de resfriamento pendentes.",x-3.6+i*1.42,y-2.15,1.0,90);
                AddGISFamilyInstance(doc,symbols,"Transformer_Fan",tag+"-FAN-"+(i+1).ToString("00"),"Ventilador representativo; potência e lógica de operação pendentes.",x-3.4+i*1.35,y-2.55,1.2,90);
            }
            for(int i=0;i<3;i++)
            {
                AddGISFamilyInstance(doc,symbols,"Transformer_BushingHV",tag+"-B-HV-"+(i+1),"Bucha AT estimada; classe dielétrica e terminal pendentes.",x-1.45+i*1.45,y+1.1,4.2);
                AddGISFamilyInstance(doc,symbols,"Transformer_BushingMV",tag+"-B-MV-"+(i+1),"Bucha MT estimada; classe dielétrica e terminal pendentes.",x-1.05+i*1.05,y-1.1,3.7);
            }
            AddGISFamilyInstance(doc,symbols,"Transformer_Conservator",tag+"-CONS","Conservador condicional; confirmar construção e acessórios do transformador.",x,y+1.9,4.15);
            AddGISFamilyInstance(doc,symbols,"ProtectionPanel",tag+"-CTRL","Painel local do transformador; funções 63/64REF/87T são candidatas, sem ajustes.",x+3.7,y,0.05);
        }

        private static void AddGISCableRoute(Document doc,Dictionary<string,FamilySymbol> symbols,string tag,double x1,double y1,double x2,double y2,double z)
        {
            double dx=x2-x1,dy=y2-y1,length=Math.Sqrt(dx*dx+dy*dy),angle=Math.Atan2(dy,dx)*180.0/Math.PI;
            FamilySymbol symbol=symbols["CableTrench"];
            double segment=1.45;
            for(double offset=segment/2.0;offset<length;offset+=segment)
            {
                double x=x1+dx*offset/length,y=y1+dy*offset/length;
                AddGISFamilyInstance(doc,symbols,"CableTrench",tag+"-"+(Math.Round(offset,1)).ToString("00.0"),"Canaleta conceitual; largura, segregação, drenagem e ocupação pendentes.",x,y,z,angle);
            }
            Log("GIS_TRENCH_ROUTE " + tag + " length_m="+length.ToString("0.0"));
        }

        private static void AddGISCableTrayRoute(Document doc,Dictionary<string,FamilySymbol> symbols,string tag,double x1,double y1,double x2,double y2,double z)
        {
            XYZ start=new XYZ(M(x1),M(y1),M(z)),end=new XYZ(M(x2),M(y2),M(z));
            if(start.DistanceTo(end)<M(.10))throw new ArgumentException("A rota de leito precisa ter comprimento positivo.",tag);
            Level level=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x=>Math.Abs(x.Elevation-M(z))).First();
            Autodesk.Revit.DB.Electrical.CableTrayType type=new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.CableTrayType)).Cast<Autodesk.Revit.DB.Electrical.CableTrayType>().FirstOrDefault();
            if(type==null)throw new InvalidOperationException("O template nao contem tipo nativo CableTray para a rota "+tag);
            Autodesk.Revit.DB.Electrical.CableTray tray=Autodesk.Revit.DB.Electrical.CableTray.Create(doc,type.Id,start,end,level.Id);
            Parameter mark=tray.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);if(mark!=null&&!mark.IsReadOnly)mark.Set(tag);
            Parameter comments=tray.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);if(comments!=null&&!comments.IsReadOnly)comments.Set("Elemento nativo CableTray, trecho reto. Apenas infraestrutura; sem cabos, preenchimento, circuitos ou conexao aos equipamentos.");
            Parameter width=tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);if(width!=null&&!width.IsReadOnly)width.Set(M(.60));
            Parameter height=tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);if(height!=null&&!height.IsReadOnly)height.Set(M(.10));
            Log("GIS_NATIVE_CABLETRAY_CREATED mark="+tag+" id="+tray.Id.Value+" length_m="+UnitUtils.ConvertFromInternalUnits(start.DistanceTo(end),UnitTypeId.Meters).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" end_connectors_open=true");
        }

        private static void AddGISCableTrayPath(Document doc,Dictionary<string,FamilySymbol> symbols,string tag,IList<XYZ> points)
        {
            if(points==null||points.Count<2)throw new ArgumentException("A rota nativa CableTray precisa de pelo menos dois pontos.",tag);
            Level level=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x=>Math.Abs(x.Elevation-points[0].Z)).First();
            Autodesk.Revit.DB.Electrical.CableTrayType type=new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.CableTrayType)).Cast<Autodesk.Revit.DB.Electrical.CableTrayType>().FirstOrDefault();
            if(type==null)throw new InvalidOperationException("O template nao contem tipo nativo CableTray para a rota "+tag);
            List<Autodesk.Revit.DB.Electrical.CableTray> segments=new List<Autodesk.Revit.DB.Electrical.CableTray>();
            for(int i=0;i<points.Count-1;i++)
            {
                if(points[i].DistanceTo(points[i+1])<M(.10))throw new ArgumentException("A rota nativa CableTray tem segmentos menores que 100 mm.",tag+"-SEG-"+(i+1).ToString("00"));
                Autodesk.Revit.DB.Electrical.CableTray tray=Autodesk.Revit.DB.Electrical.CableTray.Create(doc,type.Id,points[i],points[i+1],level.Id);
                string segmentMark=tag+"-SEG-"+(i+1).ToString("00");
                Parameter mark=tray.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);if(mark!=null&&!mark.IsReadOnly)mark.Set(segmentMark);
                Parameter comments=tray.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);if(comments!=null&&!comments.IsReadOnly)comments.Set("Trecho nativo CableTray conectado apenas a trechos de infraestrutura adjacentes quando registrado. Sem condutores, circuito, preenchimento ou conexao eletrica ao equipamento.");
                Parameter width=tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);if(width!=null&&!width.IsReadOnly)width.Set(M(.60));
                Parameter height=tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);if(height!=null&&!height.IsReadOnly)height.Set(M(.10));
                segments.Add(tray);
            }
            for(int i=0;i<segments.Count-1;i++)
            {
                Autodesk.Revit.DB.Electrical.CableTray first=segments[i],second=segments[i+1];XYZ joint=points[i+1];
                Connector firstEnd=first.ConnectorManager.Connectors.Cast<Connector>().OrderBy(x=>x.Origin.DistanceTo(joint)).FirstOrDefault();
                Connector secondEnd=second.ConnectorManager.Connectors.Cast<Connector>().OrderBy(x=>x.Origin.DistanceTo(joint)).FirstOrDefault();
                using(SubTransaction attempt=new SubTransaction(doc))
                {
                    attempt.Start();
                    try
                    {
                        if(firstEnd==null||secondEnd==null)throw new InvalidOperationException("Porta de leito ausente na junta.");
                        firstEnd.ConnectTo(secondEnd);doc.Regenerate();
                        bool connected=firstEnd.IsConnected&&secondEnd.IsConnected;
                        if(!connected)throw new InvalidOperationException("ConnectTo nao confirmou a junta dos leitos.");
                        attempt.Commit();Log("GIS_CABLETRAY_JOINT_CONNECTED route="+tag+" at="+i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    catch(Exception ex)
                    {
                        if(attempt.GetStatus()==TransactionStatus.Started)attempt.RollBack();
                        Log("GIS_CABLETRAY_JOINT_FAILED route="+tag+" at="+i.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "+ex.GetType().Name+" "+ex.Message);
                    }
                }
            }
            Log("GIS_NATIVE_CABLETRAY_PATH_CREATED route="+tag+" segments="+segments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)+" length_m="+segments.Sum(x=>((LocationCurve)x.Location).Curve.Length).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AddGISGroundGrid(Document doc,double minX,double maxX,double minY,double maxY,double spacing)
        {
            GraphicsStyle grid=GetLineStyle(doc,"GIS-R9-MALHA-ATERRAMENTO",new Color(112,126,91),1);
            for(double x=minX;x<=maxX+.001;x+=spacing) AddGISModelLine(doc,new XYZ(M(x),M(minY),M(.06)),new XYZ(M(x),M(maxY),M(.06)),grid);
            for(double y=minY;y<=maxY+.001;y+=spacing) AddGISModelLine(doc,new XYZ(M(minX),M(y),M(.06)),new XYZ(M(maxX),M(y),M(.06)),grid);
            Log("GIS_GROUND_GRID_CONCEPTUAL spacing_m="+spacing.ToString("0.0")+"; no soil study");
        }

        private static void AddGISModelLine(Document doc,XYZ start,XYZ end,GraphicsStyle style=null)
        {
            XYZ direction=(end-start).Normalize();XYZ normal=direction.CrossProduct(XYZ.BasisZ);
            if(normal.GetLength()<1e-8) normal=direction.CrossProduct(XYZ.BasisX);
            SketchPlane plane=SketchPlane.Create(doc,Plane.CreateByNormalAndOrigin(normal.Normalize(),start));
            ModelCurve modelCurve=doc.Create.NewModelCurve(Line.CreateBound(start,end),plane);
            if(style!=null)modelCurve.LineStyle=style;
        }

        private static void AddGISFence(Document doc,double minX,double maxX,double minY,double maxY)
        {
            foreach(double z in new[]{.55,1.25,2.0})
            {
                AddGISModelLine(doc,new XYZ(M(minX),M(minY),M(z)),new XYZ(M(31),M(minY),M(z)));
                AddGISModelLine(doc,new XYZ(M(37),M(minY),M(z)),new XYZ(M(maxX),M(minY),M(z)));
                AddGISModelLine(doc,new XYZ(M(minX),M(minY),M(z)),new XYZ(M(minX),M(maxY),M(z)));
                AddGISModelLine(doc,new XYZ(M(maxX),M(minY),M(z)),new XYZ(M(maxX),M(maxY),M(z)));
                AddGISModelLine(doc,new XYZ(M(minX),M(maxY),M(z)),new XYZ(M(maxX),M(maxY),M(z)));
            }
            for(double x=minX;x<=maxX+.001;x+=2.4)
            {
                if(x<31||x>37)AddGISModelLine(doc,new XYZ(M(x),M(minY),M(.05)),new XYZ(M(x),M(minY),M(2.15)));
                AddGISModelLine(doc,new XYZ(M(x),M(maxY),M(.05)),new XYZ(M(x),M(maxY),M(2.15)));
            }
            for(double y=minY;y<=maxY+.001;y+=2.4)
            {
                AddGISModelLine(doc,new XYZ(M(minX),M(y),M(.05)),new XYZ(M(minX),M(y),M(2.15)));
                AddGISModelLine(doc,new XYZ(M(maxX),M(y),M(.05)),new XYZ(M(maxX),M(y),M(2.15)));
            }
            Log("GIS_FENCE_CREATED conceptual galvanized rails and posts; access gate gap 6 m");
        }

        private static void AddGISPrimaryRoute(Document doc,string mark,string description,XYZ start,XYZ end,double radius,Material material)
        {
            XYZ axis=end-start;double length=axis.GetLength();if(length<M(.02))return;
            XYZ direction=axis.Normalize();XYZ guide=Math.Abs(direction.DotProduct(XYZ.BasisZ))<.90?XYZ.BasisZ:XYZ.BasisY;
            XYZ u=direction.CrossProduct(guide).Normalize();XYZ v=direction.CrossProduct(u).Normalize();
            CurveLoop loop=new CurveLoop();const int sides=20;
            for(int i=0;i<sides;i++)
            {
                double a=2*Math.PI*i/sides,b=2*Math.PI*(i+1)/sides;
                XYZ p1=start+u*(M(radius)*Math.Cos(a))+v*(M(radius)*Math.Sin(a));
                XYZ p2=start+u*(M(radius)*Math.Cos(b))+v*(M(radius)*Math.Sin(b));
                loop.Append(Line.CreateBound(p1,p2));
            }
            Solid solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{loop},direction,length);
            DirectShape shape=DirectShape.CreateElement(doc,new ElementId(BuiltInCategory.OST_GenericModel));
            shape.ApplicationId="PortfolioBIM.GIS.R9.GeometryOnly";shape.ApplicationDataId=mark+"-"+Guid.NewGuid().ToString("N");shape.SetShape(new List<GeometryObject>{solid});
            Parameter p=shape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);if(p!=null&&!p.IsReadOnly)p.Set(mark);
            p=shape.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);if(p!=null&&!p.IsReadOnly)p.Set(description+" REPRESENTACAO GEOMETRICA; sem conector eletrico, contato interno ou circuito Revit.");
            p=shape.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);if(material!=null&&p!=null&&!p.IsReadOnly)p.Set(material.Id);
        }

        private static void AddGISPrimaryRoutePolyline(Document doc,string mark,string description,IList<XYZ> points,double radius,Material material)
        {
            for(int i=0;i<points.Count-1;i++)AddGISPrimaryRoute(doc,mark+"-SEG-"+(i+1).ToString("00"),description,points[i],points[i+1],radius,material);
        }

        private static ViewPlan CreateGISPlan(Document doc,Level level,string name,double x1,double y1,double x2,double y2,int scale)
        {
            ViewPlan view=CreatePlan(doc,level,name,x2-x1,y2-y1,scale);
            view.CropBox=new BoundingBoxXYZ{Min=new XYZ(M(x1),M(y1),level.Elevation-M(.2)),Max=new XYZ(M(x2),M(y2),level.Elevation+M(5.8))};
            view.CropBoxActive=true;view.DetailLevel=ViewDetailLevel.Fine;
            if(!name.StartsWith("E-05",StringComparison.OrdinalIgnoreCase))
            {
                Category lineRoot=doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                Category groundGrid=lineRoot.SubCategories.Cast<Category>().FirstOrDefault(x=>x.Name=="GIS-R9-MALHA-ATERRAMENTO");
                if(groundGrid!=null)try{view.SetCategoryHidden(groundGrid.Id,true);}catch(Exception ex){Log("GIS_GROUND_GRID_PLAN_HIDE_NOTE "+name+" "+ex.Message);}
            }
            return view;
        }

        private static View3D CreateGIS3DView(Document doc,string name,double x1,double y1,double z1,double x2,double y2,double z2)
        {
            ViewFamilyType type=new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(x=>x.ViewFamily==ViewFamily.ThreeDimensional);
            View3D view=View3D.CreateIsometric(doc,type.Id);view.Name=name;view.DetailLevel=ViewDetailLevel.Fine;view.DisplayStyle=DisplayStyle.Shading;
            view.SetSectionBox(new BoundingBoxXYZ{Min=new XYZ(M(x1),M(y1),M(z1)),Max=new XYZ(M(x2),M(y2),M(z2))});view.IsSectionBoxActive=true;
            foreach(BuiltInCategory category in new[]{BuiltInCategory.OST_Levels,BuiltInCategory.OST_Grids})try{view.SetCategoryHidden(new ElementId(category),true);}catch{}
            try
            {
                FillPatternElement solid=new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().First(x=>x.GetFillPattern().IsSolidFill);
                OverrideGraphicSettings electrical=new OverrideGraphicSettings();
                electrical.SetSurfaceForegroundPatternId(solid.Id);electrical.SetSurfaceForegroundPatternColor(new Color(91,120,139));electrical.SetProjectionLineColor(new Color(43,59,68));
                view.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_ElectricalEquipment),electrical);
                OverrideGraphicSettings generic=new OverrideGraphicSettings();
                generic.SetSurfaceForegroundPatternId(solid.Id);generic.SetSurfaceForegroundPatternColor(new Color(136,147,151));generic.SetProjectionLineColor(new Color(69,80,84));
                view.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_GenericModel),generic);
            }
            catch(Exception ex){Log("GIS_3D_EQUIPMENT_COLOR_NOTE "+name+" "+ex.Message);}
            if(!name.StartsWith("3D-01",StringComparison.OrdinalIgnoreCase))
            {
                Category lineRoot=doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                Category groundGrid=lineRoot.SubCategories.Cast<Category>().FirstOrDefault(x=>x.Name=="GIS-R9-MALHA-ATERRAMENTO");
                if(groundGrid!=null)try{view.SetCategoryHidden(groundGrid.Id,true);}catch{}
            }
            return view;
        }

        private static void CreateGISSheets(Application app,Document doc,ProjectSpec spec,string outDir,string baseName,List<ElementId> sheetIds,IList<ElementId> roofIds)
        {
            using(Transaction tx=new Transaction(doc,"Pranchas de engenharia GIS R9"))
            {
                tx.Start();
                Level level=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x=>Math.Abs(x.Elevation)).First();
                ElementId text=GetTextTypeId(doc);
                GraphicsStyle note=GetLineStyle(doc,"GIS-R9-ANOTACOES",new Color(42,52,59),2);
                GraphicsStyle power=GetLineStyle(doc,"GIS-R9-POTENCIA",new Color(45,94,144),4);
                GraphicsStyle data=GetLineStyle(doc,"GIS-R9-CONTROLE",new Color(34,124,99),3);
                ViewPlan site=CreateGISPlan(doc,level,"A-01 - Implantacao geral R9",0,0,72,56,300);
                ViewPlan architecture=CreateGISPlan(doc,level,"A-02 - Edificios e areas de manutencao R9",5,7,67,50,225);
                ViewPlan gis=CreateGISPlan(doc,level,"E-02 - Arranjo GIS referencia 145 kV R9",6,8,39,28,125);
                ViewPlan transformers=CreateGISPlan(doc,level,"E-03 - Transformador e conexoes R9",26,28,61,42,125);
                ViewPlan mv=CreateGISPlan(doc,level,"E-04 - Secao MT 13,8 kV R9",40,8,66,28,125);
                ViewPlan routes=CreateGISPlan(doc,level,"E-05 - Rotas de infraestrutura R9",5,8,67,49,250);
                ViewPlan auxiliaryPlan=CreateGISPlan(doc,level,"E-06 - Servicos auxiliares e controle R9",5,32,31,50,150);
                TryCreateGISAuxWire(doc,auxiliaryPlan);
                View3D overall=CreateGIS3DView(doc,"3D-01 - Implantacao BIM geral R9",2,2,-.3,70,54,7.5);
                View3D gisBay=CreateGIS3DView(doc,"3D-02 - Bay GIS-T01 e selecao de barras R9",17.2,14.9,-.1,20.3,22.9,4.2);
                View3D transformer3D=CreateGIS3DView(doc,"3D-03 - Transformador TR-01 R9",27.0,29.0,-.2,38.0,41.8,7.0);
                View3D mv3D=CreateGIS3DView(doc,"3D-04 - Cubiculos secao A MT R9",42.8,9.0,-.1,50.2,18.8,4.2);
                foreach(View3D interior in new[]{overall,gisBay,transformer3D,mv3D})
                {
                    if(interior!=overall)try{interior.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Walls),true);}catch{}
                    else try{OverrideGraphicSettings walls=new OverrideGraphicSettings();walls.SetSurfaceTransparency(72);interior.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_Walls),walls);}catch{}
                    try{interior.HideElements(roofIds);}catch(Exception ex){Log("GIS_ROOF_HIDE_NOTE "+interior.Name+" "+ex.Message);}
                }
                ViewDrafting single=CreateDraftingView(doc,"E-01 - Unifilar geral 138 e 13,8 kV R9");single.Scale=15;
                ViewDrafting protection=CreateDraftingView(doc,"E-06 - Protecao e servicos auxiliares R9");
                ViewDrafting calculations=CreateDraftingView(doc,"M-01 - Estudos eletricos e premissas R9");
                ViewDrafting quantities=CreateDraftingView(doc,"Q-01 - Quantitativos e conectividade R9");
                AddGISPlanNote(doc,site,text,11,29,"GIS 145 kV | cinco bays | dupla barra");
                AddGISPlanNote(doc,site,text,44,28,"CUBICULOS MT 13,8 kV | secoes A/B");
                AddGISPlanNote(doc,site,text,29,42,"TR-01 | 40 MVA | envelope estimado");
                AddGISPlanNote(doc,site,text,48,42,"TR-02 | 40 MVA | envelope estimado");
                AddGISPlanNote(doc,site,text,8,50,"CASA DE CONTROLE / PROTECAO / TELECOM");
                AddGISPlanNote(doc,architecture,text,8.5,25.5,"GIS | envelope do bay de referencia e zona de acesso; afastamentos pendentes");
                AddGISPlanNote(doc,architecture,text,43,25.5,"MT | corredor de operacao e extracao de cubiculos a verificar");
                AddGISPlanNote(doc,architecture,text,8,47,"CONTROLE / RELES / TELECOM");
                AddGISPlanNote(doc,architecture,text,19,47,"BATERIAS / AUXILIARES");
                AddGISPlanNote(doc,gis,text,17.6,25.5,"DETALHE: GIS-T01 | 89A/89B | 52 | TC | rota GIS-TR01");
                AddGISPlanNote(doc,transformers,text,28,40.5,"TR-01 | tanque, radiadores, ventiladores, buchas, conservador condicional e contencao");
                AddGISPlanNote(doc,transformers,text,47,40.5,"TR-02 | dimensoes do fabricante nao publicadas");
                AddGISPlanNote(doc,mv,text,42.2,25.5,"A-IN / MED / F01...F05 | entrada TR-01 e saidas representadas no unifilar");
                AddGISPlanNote(doc,routes,text,8,30,"ROTAS: GIS/MT/transformadores/controle | leitos nativos e envelopes | sem condutores");
                AddGISPlanNote(doc,auxiliaryPlan,text,8,48,"AUX-UPS-01 | ElectricalSystem + fio 2D Revit, se conectores confirmados | nao e cabo 3D");
                DrawGISSingleLine(doc,single,text,note,power);
                DrawGISProtectionArchitecture(doc,protection,text,note,power,data);
                DrawGISReconstructionCalculationTable(doc,calculations,text,note,power,data);
                DrawGISQuantitySummary(doc,quantities,text);

                ElementId titleBlock=EnsureTitleBlock(doc,app,outDir);
                sheetIds.Add(CreateSheet(doc,titleBlock,"A-01","Implantacao e acessos",site,text,"LOTE DE ESTUDO\n72 x 56 m. Edificios, transformadores, acessos, rotas de infraestrutura e malha conceitual.\n\nPENDENTE\nLocacao, afastamentos, drenagem, incêndio, ventilação, circulação de manutenção e requisitos da concessionária.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"A-02","Edificios e zonas operacionais",architecture,text,"PAREDES E PISOS REVIT\nSetores GIS, média tensão e comando. A vista amplia a implantação dos edifícios e áreas de operação.\n\nPENDENTE\nRotas de fuga, acessibilidade, ventilação, incêndio e afastamentos de manutenção.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"E-01","Unifilar de potência 138 / 13,8 kV",single,text,"TOPOLOGIA\nBarras A/B em 138 kV; cada bay usa seletores 89A/89B; o acoplador 52-BC fica normalmente aberto como hipótese. TR-01 alimenta a seção A; TR-02, a seção B; 52-TIE MT normalmente aberto.\n\nC=fechado; O=aberto. Estados e intertravamentos são premissas a validar.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"E-02","Bay GIS de referência 145 kV",gis,text,"REFERÊNCIA\nGE Vernova F35: classe 145 kV e módulo de bay 800 mm. O envelope 5,7 m e o arranjo dos elementos são aproximações; não há desenho GA do conjunto selecionado.\n\nMODELO\nCarcaças e stubs geométricos; contato interno, isolamento, rating, conectores elétricos e estados Revit não modelados.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"E-03","Transformadores e terminais",transformers,text,"REFERÊNCIA\nDAX TRP-EA40-DML-01: 138/13,8 kV, 30/40 MVA, Dyn1, LTC, Z=11%; referência não selecionada e desenho de contorno não publicado. O estudo preserva Z=10% provisória.\n\nMODELO\nTanque/acessórios estimados; transição HV/MV trifásica é DirectShape geométrico até as buchas/entrada traseira, sem condutor, interface elétrica ou cabo no Revit.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"E-04","Cubículos MT, seção A",mv,text,"REFERÊNCIA\nABB UniGear ZS1 17,5 kV: largura 800 mm e profundidade 1390 mm; altura depende do compartimento LV. Modelo externo aproximado, sem detalhes internos proprietários.\n\nPENDENTE\nRating aprovado, arco interno, cabos, terminações, extração e intertravamentos.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"E-05","Infraestrutura e caminhos",routes,text,"ELEMENTOS NATIVOS\nLeitos CableTray nativos em segmentos com juntas via ConnectTo quando confirmadas pela API; envelopes DirectShape separados por fase indicam as transições HV/MV. Nenhum deles constitui condutor/circuito de potência.\n\nPENDENTE\nCabos, terminações, suportação, ocupação, segregação, ampacidade e juntas não confirmadas na auditoria.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"E-06","Proteção e serviços auxiliares",protection,text,"SISTEMA AUXILIAR\nAUX-UPS-01: ElectricalSystem entre QAC-01 e UPS-01 e fio nativo 2D no plano E-06, se os dois conectores forem confirmados na extração. O fio é view-specific, não é cabo 3D nem prova alimentação completa. QCC, bateria e demais cargas seguem sem circuitos artificiais.\n\nPENDENTE\nMissão DC/UPS, cargas, ajustes, protocolos, proteção e seletividade.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"M-01","Cálculos e sensibilidades",calculations,text,"R8 PRESERVADA\nBP-01 a BP-15 e correntes FDR-* mantidas sem alteração.\n\nR9\nSensibilidades de fonte curta, impedância provisória do transformador e cabo candidato; nenhum resultado seleciona cabo, disjuntor ou ajuste.",spec,baseName));
                sheetIds.Add(CreateSheet(doc,titleBlock,"Q-01","Equipamentos e conectividade",quantities,text,"EXTRAÇÃO\nQuantitativos e conectores devem ser lidos do documento aberto no Revit 2027.\n\nARQUIVOS\nCSV por equipamento, conectores AllRefs/IsConnected, sistemas elétricos, tray nativos, rotas geométricas e relações não implementadas.",spec,baseName));
                sheetIds.Add(CreateGISPerspectiveSheet(doc,titleBlock,"BIM-01","Implantação BIM geral",overall,text,"Visão geral da subestação e dos principais edifícios e equipamentos.",spec));
                sheetIds.Add(CreateGISPerspectiveSheet(doc,titleBlock,"BIM-02","Bay GIS-T01 e seletor de barras",gisBay,text,"Um bay completo em escala espacial ampliada, com módulo de barra A/B, chaves seletoras, disjuntor, TC e envelope de transição. Geometria aproximada; contatos internos não expostos.",spec));
                sheetIds.Add(CreateGISPerspectiveSheet(doc,titleBlock,"BIM-03","Transformador TR-01 e acessórios",transformer3D,text,"Tanque externo, buchas, radiadores, ventiladores, base e contenção; dimensões do contorno estimadas por ausência de desenho de fabricante.",spec));
                sheetIds.Add(CreateGISPerspectiveSheet(doc,titleBlock,"BIM-04","Cubículos MT seção A",mv3D,text,"Envelope externo e organização de uma seção de cubículos ABB UniGear de referência; compartimentos internos não modelados.",spec));
                doc.Regenerate();
                if(tx.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("A transação de folhas GIS não foi confirmada.");
            }
            Log("GIS_SHEETS_CREATED count="+sheetIds.Count);
        }

        private static void AddGISPlanNote(Document doc,View view,ElementId text,double x,double y,string value)
        {
            TextNote.Create(doc,view.Id,XY(x,y,0),value,text);
        }

        private static void DrawGISQuantitySummary(Document doc,ViewDrafting view,ElementId text)
        {
            List<FamilyInstance> instances=new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(x=>x.Category!=null&&(x.Category.Id==new ElementId(BuiltInCategory.OST_ElectricalEquipment)||x.Category.Id==new ElementId(BuiltInCategory.OST_GenericModel)))
                .ToList();
            var rows=instances.GroupBy(x=>new
                {
                    Category=x.Category.Id==new ElementId(BuiltInCategory.OST_ElectricalEquipment)?"EQ. ELETRICO":"MODELO GENERICO",
                    Family=x.Symbol.Family.Name,
                    System=GetGISParameter(x,"BIM_Sistema_Elemento"),
                    State=GetGISParameter(x.Symbol,"BIM_EstadoGeometria")
                })
                .OrderBy(x=>x.Key.Category).ThenBy(x=>x.Key.Family).ToList();
            GraphicsStyle line=GetLineStyle(doc,"GIS-R9-QUANTITATIVOS",new Color(120,132,139),1);
            TextNote.Create(doc,view.Id,XY(.35,10.5,0),"QUANTITATIVOS DO DOCUMENTO REVIT | INSTANCIAS FAMILYINSTANCE AGRUPADAS POR FAMILIA / TIPO",text);
            TextNote.Create(doc,view.Id,XY(.40,9.85,0),"CATEGORIA",text);
            TextNote.Create(doc,view.Id,XY(3.2,9.85,0),"FAMILIA",text);
            TextNote.Create(doc,view.Id,XY(11.2,9.85,0),"SISTEMA",text);
            TextNote.Create(doc,view.Id,XY(15.1,9.85,0),"QTD.",text);
            TextNote.Create(doc,view.Id,XY(16.1,9.85,0),"GEOMETRIA",text);
            DrawLine(doc,view,.35,9.62,18.6,9.62,line,0);
            double y=9.34;
            const double rowStep=.34;
            foreach(var row in rows)
            {
                TextNote.Create(doc,view.Id,XY(.40,y,0),row.Key.Category,text);
                TextNote.Create(doc,view.Id,XY(3.2,y,0),row.Key.Family,text);
                TextNote.Create(doc,view.Id,XY(11.2,y,0),CompactGISSystemLabel(row.Key.System),text);
                TextNote.Create(doc,view.Id,XY(15.1,y,0),row.Count().ToString(System.Globalization.CultureInfo.InvariantCulture),text);
                TextNote.Create(doc,view.Id,XY(16.1,y,0),row.Key.State,text);
                y-=rowStep;
            }
            DrawLine(doc,view,.35,y-.16,18.6,y-.16,line,0);
            TextNote.Create(doc,view.Id,XY(.40,y-.42,0),"CSV R9: lista por instância, parâmetros, conectores e estados IsConnected/AllRefs; DirectShapes e CableTray são auditados separadamente.",text);
            if(rows.Count==0)throw new InvalidOperationException("Q-01 nao encontrou instancias BIM para contar.");
        }

        private static string CompactGISSystemLabel(string value)
        {
            if(value=="GIS_145K")return "GIS 138 kV";
            if(value=="TRANSFORMACAO_138_13K8")return "TRANSF.";
            if(value=="DISTRIBUICAO_13K8")return "MT 13,8 kV";
            if(value=="CONTROLE_SERVICOS_AUXILIARES")return "AUX / CTRL";
            return value;
        }

        private static bool CreateGISControlledExplodedView(Document doc,View3D view)
        {
            List<FamilyInstance> instances=new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
            string Mark(FamilyInstance instance)=>instance.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()??"";
            var definitions=new[]
            {
                new{Label="estrutura do bay",Predicate=(Func<string,bool>)(x=>x=="GIS-T01-EST"),Offset=new XYZ(-M(.7),0,0)},
                new{Label="disjuntor",Predicate=(Func<string,bool>)(x=>x=="GIS-T01-52"),Offset=new XYZ(M(.9),0,0)},
                new{Label="seccionadoras",Predicate=(Func<string,bool>)(x=>x=="GIS-T01-89A"||x=="GIS-T01-89B"),Offset=new XYZ(0,M(.8),0)},
                new{Label="chave de aterramento",Predicate=(Func<string,bool>)(x=>x=="GIS-T01-89G"),Offset=new XYZ(0,-M(.8),0)},
                new{Label="transformador de corrente",Predicate=(Func<string,bool>)(x=>x=="GIS-T01-CT"),Offset=new XYZ(0,0,M(.7))}
            };
            var prepared=new List<(string Label,List<ElementId> Ids,XYZ Offset,Reference Edge)>();
            List<ElementId> created=new List<ElementId>();
            try
            {
                foreach(var definition in definitions)
                {
                    List<ElementId> ids=instances.Where(x=>definition.Predicate(Mark(x))).Select(x=>x.Id).ToList();
                    if(ids.Count==0)throw new InvalidOperationException("Componente da vista explodida ausente: "+definition.Label);
                    if(!DisplacementElement.CanElementsBeDisplaced(view,ids))throw new InvalidOperationException("Revit nao permite deslocar: "+definition.Label);
                    Reference edge=FindGISFamilyEdgeReference(doc.GetElement(ids[0]));
                    if(edge==null)throw new InvalidOperationException("Nao foi possivel obter referencia geometrica para o caminho: "+definition.Label);
                    prepared.Add((definition.Label,ids,definition.Offset,edge));
                }
                foreach(var item in prepared)
                {
                    DisplacementElement displacement=DisplacementElement.Create(doc,item.Ids,item.Offset,view,null);
                    created.Add(displacement.Id);
                    ElementId path=DisplacementPath.Create(doc,displacement,item.Edge,.5);
                    created.Add(path);
                    Log("GIS_EXPLODED_GROUP "+item.Label+" ids="+item.Ids.Count+" offset_m="+UnitUtils.ConvertFromInternalUnits(item.Offset.X,UnitTypeId.Meters).ToString("0.00")+","+UnitUtils.ConvertFromInternalUnits(item.Offset.Y,UnitTypeId.Meters).ToString("0.00")+","+UnitUtils.ConvertFromInternalUnits(item.Offset.Z,UnitTypeId.Meters).ToString("0.00"));
                }
                doc.Regenerate();
                return true;
            }
            catch(Exception ex)
            {
                try{if(created.Count>0)doc.Delete(created);}catch(Exception cleanup){Log("GIS_EXPLODED_CLEANUP_NOTE "+cleanup.Message);}
                Log("GIS_EXPLODED_VIEW_PENDING "+ex);
                return false;
            }
        }

        private static Reference FindGISFamilyEdgeReference(Element element)
        {
            if(element==null)return null;
            Options options=new Options{ComputeReferences=true,IncludeNonVisibleObjects=true,DetailLevel=ViewDetailLevel.Fine};
            GeometryElement geometry=element.get_Geometry(options);
            foreach(GeometryObject item in geometry)
            {
                Solid solid=item as Solid;
                if(solid!=null)foreach(Edge edge in solid.Edges)if(edge.Reference!=null)return edge.Reference;
                GeometryInstance instance=item as GeometryInstance;
                if(instance!=null)
                    foreach(GeometryObject nested in instance.GetSymbolGeometry())
                    {
                        Solid nestedSolid=nested as Solid;
                        if(nestedSolid!=null)foreach(Edge edge in nestedSolid.Edges)if(edge.Reference!=null)return edge.Reference;
                    }
            }
            return null;
        }

        private static void DrawGISAuxiliaryDiagram(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power,GraphicsStyle data)
        {
            TextNote.Create(doc,view.Id,XY(.35,9.8,0),"SERVICOS AUXILIARES | DIAGRAMA FUNCIONAL CA / CC | SEM ESCALA",text);
            Box(doc,view,.5,7.4,4.5,8.6,"FONTE CA NORMAL\norigem e disponibilidade pendentes",text,note,0);
            Box(doc,view,6.5,7.4,10.5,8.6,"QAC-01\nquadro auxiliar CA",text,power,0);
            DrawLine(doc,view,4.5,8.0,6.5,8.0,power,0);
            Box(doc,view,12.5,7.4,16.5,8.6,"QCC-01\nquadro auxiliar CC",text,power,0);
            Box(doc,view,.5,4.7,4.5,5.9,"CARGAS CA\nclimatizacao / iluminacao\nacionamentos auxiliares",text,note,0);
            Box(doc,view,6.5,4.7,10.5,5.9,"UPS-01\npotencia e autonomia\npendentes",text,power,0);
            Box(doc,view,12.5,4.7,16.5,5.9,"BAT-01\nmissao e Ah\npendentes",text,power,0);
            DrawLine(doc,view,8.5,7.4,8.5,5.9,power,0);
            DrawLine(doc,view,14.5,7.4,14.5,5.9,power,0);
            DrawLine(doc,view,10.5,5.3,12.5,5.3,power,0);
            Box(doc,view,.5,2.0,4.5,3.2,"PROTECAO / IEDs\nfonte CC",text,data,0);
            Box(doc,view,6.5,2.0,10.5,3.2,"CONTROLE / SCADA\nUPS",text,data,0);
            Box(doc,view,12.5,2.0,16.5,3.2,"COMUNICACAO / FIBRA\nUPS",text,data,0);
            DrawLine(doc,view,14.5,4.7,14.5,3.2,power,0);
            DrawLine(doc,view,4.5,2.6,6.5,2.6,data,0);
            DrawLine(doc,view,10.5,2.6,12.5,2.6,data,0);
            TextNote.Create(doc,view.Id,XY(.5,.9,0),"Arquitetura esquemática; fontes redundantes, aterramento DC, autonomia, queda de tensão e coordenação ainda sem dados.",text);
        }

        private static void DrawGISReconstructionCalculationTable(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power,GraphicsStyle data)
        {
            TextNote.Create(doc,view.Id,XY(.35,10.0,0),"R8 MANTIDA + SENSIBILIDADES COMPLEMENTARES R9 | PREMISSAS EXPLICITAS",text);
            string[][] rows={
                new[]{"ID","GRANDEZA","RESULTADO","ESTADO"},
                new[]{"BP-01","S total, 56 MW / FP 0,95",GisCalc("BP-01","0.000")+" MVA","PREMISSA DE ESTUDO"},
                new[]{"BP-03","I de carga em 138 kV",GisCalc("BP-03","0.000")+" kA","PREMISSA DE ESTUDO"},
                new[]{"BP-04","I de carga em 13,8 kV",GisCalc("BP-04","0.000")+" kA","PREMISSA DE ESTUDO"},
                new[]{"BP-05/06","I nominal TR 40 MVA",GisCalc("BP-05","0.000")+" / "+GisCalc("BP-06","0.000")+" kA","PLACA PENDENTE"},
                new[]{"BP-07","Carga normal agregada",GisCalc("BP-07","0.0")+" % de 80 MVA","DIVISAO EQUILIBRADA"},
                new[]{"BP-08/09","N-1 crítica / total",GisCalc("BP-08","0.0")+" / "+GisCalc("BP-09","0.0")+" %","24 MW DE ALIVIO"},
                new[]{"BP-12/13","Icc simplificada 1 / 2 TR",GisCalc("BP-12","0.00")+" / "+GisCalc("BP-13","0.00")+" kA","Z=10% PROVISORIA"},
                new[]{"SC-1000-1TR-250M","Icc fonte+TR+cabo bus/remoto",GisR9Calc("short_circuit_sensitivities","SC-1000MVA-1TR-250M","bus_fault_current_ka_symmetric_rms","remote_fault_current_ka_symmetric_rms")+" kA","SENSIBILIDADE"},
                new[]{"CAB-06-250M","FDR 6 MW | ΔV | perda",GisR9Calc("cable_sensitivities","CAB-06MW-250M","voltage_drop_percent","three_phase_copper_loss_kw")+" % / kW","AMPACIDADE PENDENTE"},
                new[]{"BP-14","Qc idealizada FP 0,95→0,98",GisCalc("BP-14","0.00")+" MVAr","SEM HARMONICOS"},
                new[]{"PENDENTE","Proteção / solo / perdas trafo","NAO CALCULADO","DADOS DE ENTRADA AUSENTES"}
            };
            double x=.55,top=8.9,rowH=.64;
            double[] widths={1.8,5.1,4.6,5.1};
            for(int r=0;r<rows.Length;r++)
            {
                double left=x;
                for(int c=0;c<rows[r].Length;c++)
                {
                    double right=left+widths[c];
                    Box(doc,view,left,top-(r+1)*rowH,right,top-r*rowH,rows[r][c],text,r==0?power:(r==rows.Length-1?note:data),0);
                    left=right;
                }
            }
            TextNote.Create(doc,view.Id,XY(.55,.55,0),"R8: fonte ideal e Z=10% provisória. R9: fonte 1000 MVA, 1 TR e 250 m de cabo são apenas hipótese. X/R, Z real, motores, instalação/ampacidade e seletividade pendentes; sem seleção de disjuntor.",text);
        }

        private static string GisCalc(string calcId,string format)
        {
            string path=Path.Combine(Root,"projetos","19-subestacao-gis","dados","reconstrucao-r8","resultados.json");
            using(JsonDocument document=JsonDocument.Parse(File.ReadAllText(path)))
            {
                JsonElement item=document.RootElement.GetProperty("calculations").EnumerateArray().FirstOrDefault(x=>x.GetProperty("calc_id").GetString()==calcId);
                if(item.ValueKind==JsonValueKind.Undefined)return "PENDENTE";
                double value=item.GetProperty("result").GetDouble();
                return value.ToString(format,System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            }
        }

        private static string GisR9Calc(string arrayName,string calcId,string firstField,string secondField)
        {
            string path=Path.Combine(Root,"projetos","19-subestacao-gis","dados","reconstrucao-r9","estudo-complementar-r9.json");
            if(!File.Exists(path))return "PENDENTE";
            using(JsonDocument document=JsonDocument.Parse(File.ReadAllText(path)))
            {
                JsonElement item=document.RootElement.GetProperty(arrayName).EnumerateArray().FirstOrDefault(x=>x.GetProperty("calc_id").GetString()==calcId);
                if(item.ValueKind==JsonValueKind.Undefined)return "PENDENTE";
                double a=item.GetProperty(firstField).GetDouble(),b=item.GetProperty(secondField).GetDouble();
                return a.ToString("0.000",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))+" / "+b.ToString("0.000",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            }
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

        private sealed class GISFamilySpec
        {
            public string Key;
            public string TypeName;
            public bool ElectricalEquipment;
            public double Width;
            public double Depth;
            public double Height;
            public double VoltageKv;
            public double SecondaryVoltageKv;
            public double RatingMva;
            public double ImpedancePu;
            public double CurrentA;
            public string Source;
            public string GeometryStatus;
            public string Description;
        }

        private static List<GISFamilySpec> GetGISFamilySpecs()
        {
            return new List<GISFamilySpec>
            {
                new GISFamilySpec{Key="GIS_BayFrame",TypeName="GIS_SUPPORT_STUDY",Width=0.8,Depth=5.7,Height=3.6,VoltageKv=145,Source="GE Vernova F35 145 kV brochure: 800 mm bay width and 5,700 mm line-bay footprint; arrangement combined as study envelope, not selected GA",GeometryStatus="REFERENCE_DIMENSIONS_APPROXIMATE",Description="Envelope do bay de referência F35; orientação e combinação das dimensões aproximadas, desenho de arranjo geral do pedido não fornecido."},
                new GISFamilySpec{Key="GIS_CircuitBreaker",TypeName="GIS_CB_145KV_STUDY",ElectricalEquipment=true,Width=0.8,Depth=1.0,Height=2.2,VoltageKv=145,Source="GE Vernova F35 145 kV product family; device envelope estimated",GeometryStatus="ESTIMATED",Description="Elemento esquemático de disjuntor GIS; sem detalhamento interno, rating ou desenho de fabricação."},
                new GISFamilySpec{Key="GIS_Disconnector",TypeName="GIS_DS_145KV_STUDY",Width=0.8,Depth=0.8,Height=1.4,VoltageKv=145,Source="GE Vernova F35 145 kV product family; mechanism envelope estimated",GeometryStatus="ESTIMATED",Description="Seccionadora GIS com mecanismo indicativo; detalhes internos e arranjo do fabricante não representados."},
                new GISFamilySpec{Key="GIS_EarthingSwitch",TypeName="GIS_ES_145KV_STUDY",Width=0.8,Depth=0.7,Height=1.0,VoltageKv=145,Source="Portfolio estimate; no model-specific outline drawing supplied",GeometryStatus="ESTIMATED",Description="Chave de aterramento GIS conceitual; capacidade de fechamento pendente."},
                new GISFamilySpec{Key="GIS_CurrentTransformer",TypeName="GIS_CT_145KV_STUDY",ElectricalEquipment=true,Width=0.8,Depth=0.8,Height=1.4,VoltageKv=145,Source="Portfolio estimate; ratio/class pending protection study",GeometryStatus="ESTIMATED",Description="Transformador de corrente GIS; relacao, classe e nucleos nao selecionados."},
                new GISFamilySpec{Key="GIS_VoltageTransformer",TypeName="GIS_VT_145KV_STUDY",ElectricalEquipment=true,Width=0.7,Depth=0.7,Height=1.5,VoltageKv=145,Source="Portfolio estimate; ratio/class pending utility data",GeometryStatus="ESTIMATED",Description="Transformador de potencial GIS; relacao, classe e carga nao selecionadas."},
                new GISFamilySpec{Key="GIS_BusModule",TypeName="GIS_BUS_145KV_STUDY",Width=0.8,Depth=0.38,Height=0.38,VoltageKv=145,Source="GE Vernova F35 reference bay width 800 mm; enclosure OD and joints estimated",GeometryStatus="ENVELOPE_ESTIMATE",Description="Módulo de carcaça de barra GIS externa; não revela condutor interno, juntas ou rating do pedido."},
                new GISFamilySpec{Key="GIS_Termination",TypeName="GIS_TERMINATION_145KV_STUDY",ElectricalEquipment=true,Width=0.9,Depth=0.9,Height=2.0,VoltageKv=145,Source="Portfolio estimate; terminal type pending line/cable data",GeometryStatus="ESTIMATED",Description="Envelope de terminal de linha GIS, configuracao de conexao pendente."},
                new GISFamilySpec{Key="Transformer_Tank",TypeName="TR_40MVA_STUDY",ElectricalEquipment=true,Width=6.2,Depth=3.5,Height=3.8,VoltageKv=138,SecondaryVoltageKv=13.8,RatingMva=40,ImpedancePu=0.10,Source="DAX Energy TRP-EA40-DML-01 catalog reference: 138/13.8 kV, 30/40 MVA, Dyn1, LTC and Z=11%; outline not published; envelope estimated and 10% preserved R8 study assumption",GeometryStatus="ESTIMATED_NO_OUTLINE_DRAWING",Description="Transformador trifásico de referência, não selecionado. Dimensões estimadas; 10% é impedância provisória R8 e difere do Z=11% da opção de catálogo."},
                new GISFamilySpec{Key="Transformer_Radiator",TypeName="TR_RADIATOR_STUDY",Width=1.25,Depth=0.32,Height=2.4,VoltageKv=0,RatingMva=0,Source="Portfolio estimate; cooling data pending transformer datasheet",GeometryStatus="ESTIMATED",Description="Banco de radiadores conceitual; area e perda termica nao selecionadas."},
                new GISFamilySpec{Key="Transformer_Conservator",TypeName="TR_CONSERVATOR_STUDY",Width=1.7,Depth=0.75,Height=0.8,Source="Portfolio estimate; include only if conservator construction is confirmed",GeometryStatus="CONDITIONAL",Description="Conservador de oleo conceitual, condicionado a construcao com conservador."},
                new GISFamilySpec{Key="Transformer_BushingHV",TypeName="TR_BUSHING_HV_STUDY",Width=0.5,Depth=0.5,Height=1.35,VoltageKv=145,Source="Portfolio estimate; bushing datasheet pending",GeometryStatus="ESTIMATED",Description="Bucha de alta tensao conceitual; classe de isolamento e terminais pendentes."},
                new GISFamilySpec{Key="Transformer_BushingMV",TypeName="TR_BUSHING_MV_STUDY",Width=0.36,Depth=0.36,Height=0.78,VoltageKv=13.8,Source="Portfolio estimate; bushing datasheet pending",GeometryStatus="ESTIMATED",Description="Bucha de media tensao conceitual; classe de isolamento e terminais pendentes."},
                new GISFamilySpec{Key="Transformer_Fan",TypeName="TR_FAN_STUDY",Width=0.55,Depth=0.35,Height=0.55,Source="Portfolio estimate; cooling equipment datasheet pending",GeometryStatus="ESTIMATED",Description="Ventilador conceitual de radiador; potencia e comando pendentes."},
                new GISFamilySpec{Key="Transformer_Base",TypeName="TR_BASE_STUDY",Width=7.0,Depth=5.4,Height=0.35,Source="Portfolio estimate; structural/geotechnical design pending",GeometryStatus="ESTIMATED",Description="Base de concreto conceitual; projeto estrutural e geotecnico pendentes."},
                new GISFamilySpec{Key="OilContainment",TypeName="OIL_CONTAINMENT_STUDY",Width=8.2,Depth=6.5,Height=0.55,Source="Portfolio estimate; oil volume and drainage study pending",GeometryStatus="ESTIMATED",Description="Contencao conceitual de oleo; capacidade e drenagem nao dimensionadas."},
                new GISFamilySpec{Key="MV_Cubicle",TypeName="MV_17K5_2KA_STUDY",ElectricalEquipment=true,Width=0.8,Depth=1.39,Height=2.595,VoltageKv=17.5,CurrentA=2000,Source="ABB UniGear ZS1 17.5 kV product-line table: width 800 mm, depth 1390 mm, height 2200/2595 mm depending LV compartment; selected assembly and rating pending",GeometryStatus="REFERENCE_DIMENSIONS_APPROXIMATE",Description="Envelope externo de referência UniGear ZS1; sem miolo comercial, ratings ou desenho do conjunto pedido."},
                new GISFamilySpec{Key="MV_BusCoupler",TypeName="MV_TIE_17K5_STUDY",ElectricalEquipment=true,Width=0.9,Depth=1.45,Height=2.45,VoltageKv=17.5,CurrentA=2000,Source="Portfolio estimate; 2,000 A candidate only, switchgear manufacturer not selected",GeometryStatus="ESTIMATED",Description="Cubiculo de acoplamento de barras 13.8 kV; 2,000 A e candidato, normalmente aberto por premissa."},
                new GISFamilySpec{Key="MVBusModule",TypeName="MV_BUS_17K5_STUDY",Width=5.8,Depth=0.22,Height=0.22,VoltageKv=17.5,CurrentA=2000,Source="Portfolio estimate; 2,000 A candidate based on switchgear family, final bus rating pending",GeometryStatus="ESTIMATED",Description="Modulo visual de barra 13,8 kV; 2,000 A candidato, rating termico e dinamico pendentes."},
                new GISFamilySpec{Key="ProtectionPanel",TypeName="PROTECTION_PANEL_125VDC_STUDY",ElectricalEquipment=true,Width=0.8,Depth=0.65,Height=2.2,VoltageKv=.125,Source="Portfolio study assumption: 125 V DC control supply; panel design and utility philosophy pending",GeometryStatus="ESTIMATED",Description="Painel de protecao, controle e supervisao; 125 Vcc e premissa, IEDs e arquitetura funcionais pendentes."},
                new GISFamilySpec{Key="AuxiliaryPanelAC",TypeName="AUX_PANEL_380VAC_STUDY",ElectricalEquipment=true,Width=0.9,Depth=0.65,Height=2.2,VoltageKv=.380,Source="Portfolio study assumption: 380 V AC; utility and load schedule pending",GeometryStatus="ESTIMATED",Description="Quadro de servicos auxiliares CA conceitual; 380 V e premissa nao aprovada."},
                new GISFamilySpec{Key="AuxiliaryPanelDC",TypeName="AUX_PANEL_125VDC_STUDY",ElectricalEquipment=true,Width=0.9,Depth=0.65,Height=2.2,VoltageKv=.125,Source="Portfolio study assumption: 125 V DC; battery mission pending",GeometryStatus="ESTIMATED",Description="Quadro de servicos auxiliares CC conceitual; 125 V e premissa nao aprovada."},
                new GISFamilySpec{Key="UPS_Cabinet",TypeName="UPS_STUDY",ElectricalEquipment=true,Width=1.1,Depth=0.8,Height=2.0,VoltageKv=.380,SecondaryVoltageKv=.125,Source="Portfolio study assumptions: 380 V AC input and 125 V DC output; mission and vendor data pending",GeometryStatus="ESTIMATED",Description="UPS conceitual; 380 Vca de entrada e 125 Vcc de saida sao premissas, potencia e autonomia pendentes."},
                new GISFamilySpec{Key="BatteryRack",TypeName="BATTERY_RACK_125VDC_STUDY",ElectricalEquipment=true,Width=1.6,Depth=0.8,Height=1.9,VoltageKv=.125,Source="Portfolio study assumption: 125 V DC; battery mission and cell data pending",GeometryStatus="ESTIMATED",Description="Banco de baterias conceitual; 125 Vcc e premissa, Ah e autonomia nao selecionados."},
                new GISFamilySpec{Key="CableTrench",TypeName="CABLE_TRENCH_STUDY",Width=1.5,Depth=0.75,Height=0.65,Source="Portfolio estimate; cable count and civil design pending",GeometryStatus="ESTIMATED",Description="Modulo de canaleta de cabos; ocupacao, segregacao e drenagem pendentes."},
                new GISFamilySpec{Key="CableTray",TypeName="CABLE_TRAY_STUDY",Width=1.5,Depth=0.42,Height=0.16,Source="Portfolio estimate; cable schedule and support spacing pending",GeometryStatus="ESTIMATED",Description="Trecho indicativo de leito de cabos, sem dimensionamento de ocupacao."},
                new GISFamilySpec{Key="SteelSupport",TypeName="GIS_STEEL_SUPPORT_STUDY",Width=0.24,Depth=0.24,Height=2.8,Source="Portfolio estimate; structural design pending",GeometryStatus="ESTIMATED",Description="Suporte metalico conceitual; perfis, cargas e ancoragens pendentes."},
                new GISFamilySpec{Key="BuildingRoof_GIS",TypeName="ROOF_GIS_31M_EST",Width=31.6,Depth=18.6,Height=0.18,Source="Portfolio estimate; structural design, HVAC and weatherproofing pending",GeometryStatus="ESTIMATED",Description="Cobertura conceitual para GIS; estrutura, cargas, ventilacao e impermeabilizacao pendentes."},
                new GISFamilySpec{Key="BuildingRoof_MV",TypeName="ROOF_MV_23M_EST",Width=23.6,Depth=18.6,Height=0.18,Source="Portfolio estimate; structural design, HVAC and weatherproofing pending",GeometryStatus="ESTIMATED",Description="Cobertura conceitual para edificio de cubiculos; estrutura, cargas, ventilacao e impermeabilizacao pendentes."},
                new GISFamilySpec{Key="BuildingRoof_Control",TypeName="ROOF_CONTROL_22M_EST",Width=22.6,Depth=15.6,Height=0.18,Source="Portfolio estimate; structural design, HVAC and weatherproofing pending",GeometryStatus="ESTIMATED",Description="Cobertura conceitual para casa de controle; estrutura, cargas, ventilacao e impermeabilizacao pendentes."}
            };
        }

        private static void EnsureGISFamilyFiles(Application app,string outDir)
        {
            string familyDir=Path.Combine(outDir,"familias","reconstrucao-r9");
            Directory.CreateDirectory(familyDir);
            foreach(GISFamilySpec spec in GetGISFamilySpecs())
            {
                string path=Path.Combine(familyDir,GISFamilyFileStem(spec)+".rfa");
                try
                {
                    if(File.Exists(path)){Log("GIS_FAMILY_REUSED "+spec.Key+" bytes="+new FileInfo(path).Length);continue;}
                    CreateParametricGISFamily(app,spec,path);
                    Log("GIS_FAMILY_SAVED "+spec.Key+" "+new FileInfo(path).Length);
                }
                catch(Exception ex){Log("GIS_FAMILY_FAILED "+spec.Key+" "+ex);throw;}
            }
        }

        private static void CreateParametricGISFamily(Application app,GISFamilySpec spec,string path)
        {
            string template=spec.ElectricalEquipment
                ? @"C:\ProgramData\Autodesk\RVT 2027\Family Templates\Portuguese\Equipamento elétrico métrico.rft"
                : @"C:\ProgramData\Autodesk\RVT 2027\Family Templates\Portuguese\Modelo Genérico Métrico.rft";
            if(!File.Exists(template))throw new FileNotFoundException("Modelo de familia Revit nao encontrado",template);
            using(Document fdoc=app.NewFamilyDocument(template))
            {
                FamilyManager fm=fdoc.FamilyManager;
                using(Transaction tx=new Transaction(fdoc,"Familia BIM parametrica - "+spec.Key))
                {
                    tx.Start();
                    FamilyType familyType=fm.NewType(spec.TypeName);
                    FamilyParameter width=fm.AddParameter("BIM_Largura",GroupTypeId.Geometry,SpecTypeId.Length,false);
                    FamilyParameter depth=fm.AddParameter("BIM_Profundidade",GroupTypeId.Geometry,SpecTypeId.Length,false);
                    FamilyParameter height=fm.AddParameter("BIM_Altura",GroupTypeId.Geometry,SpecTypeId.Length,false);
                    FamilyParameter bodyHeight=fm.AddParameter("BIM_AlturaCorpoPrincipal",GroupTypeId.Geometry,SpecTypeId.Length,false);
                    FamilyParameter voltage=fm.AddParameter("BIM_TensaoClasse_kV",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter nominalVoltage=fm.AddParameter("BIM_TensaoNominal_kV",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter voltageSecondary=fm.AddParameter("BIM_TensaoSecundaria_kV",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter voltageSecondaryClass=fm.AddParameter("BIM_TensaoClasseSecundaria_kV",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter rating=fm.AddParameter("BIM_Potencia_MVA",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter current=fm.AddParameter("BIM_CorrenteNominal_A",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter currentSecondary=fm.AddParameter("BIM_CorrenteReferenciaSecundaria_A",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter impedance=fm.AddParameter("BIM_Impedancia_pu",GroupTypeId.IdentityData,SpecTypeId.Number,false);
                    FamilyParameter geometryState=fm.AddParameter("BIM_EstadoGeometria",GroupTypeId.IdentityData,SpecTypeId.String.Text,false);
                    FamilyParameter dataState=fm.AddParameter("BIM_EstadoEspecificacao",GroupTypeId.IdentityData,SpecTypeId.String.Text,false);
                    FamilyParameter source=fm.AddParameter("BIM_FonteGeometria",GroupTypeId.IdentityData,SpecTypeId.String.Text,false);
                    FamilyParameter description=fm.AddParameter("BIM_DescricaoEstudo",GroupTypeId.IdentityData,SpecTypeId.String.Text,false);
                    FamilyParameter manufacturer=fm.AddParameter("BIM_Fabricante",GroupTypeId.IdentityData,SpecTypeId.String.Text,false);
                    FamilyParameter reference=fm.AddParameter("BIM_ReferenciaCatalogo",GroupTypeId.IdentityData,SpecTypeId.String.Text,false);
                    fm.AddParameter("BIM_Sistema_Elemento",GroupTypeId.IdentityData,SpecTypeId.String.Text,true);
                    fm.AddParameter("BIM_Localizacao",GroupTypeId.IdentityData,SpecTypeId.String.Text,true);
                    fm.AddParameter("BIM_Bay",GroupTypeId.IdentityData,SpecTypeId.String.Text,true);
                    fm.Set(width,M(spec.Width));
                    fm.Set(depth,M(spec.Depth));
                    fm.Set(height,M(spec.Height));
                    double bodyHeightRatio=1.0;
                    if(spec.Key=="Transformer_Tank")bodyHeightRatio=.84;
                    else if(spec.Key=="GIS_CircuitBreaker")bodyHeightRatio=.20;
                    else if(spec.Key=="GIS_Disconnector"||spec.Key=="GIS_EarthingSwitch"||spec.Key=="GIS_CurrentTransformer")bodyHeightRatio=.18;
                    else if(spec.Key=="GIS_BusModule"||spec.Key=="MVBusModule")bodyHeightRatio=.22;
                    else if(spec.Key=="GIS_VoltageTransformer"||spec.Key=="GIS_Termination")bodyHeightRatio=.30;
                    else if(spec.Key=="Transformer_Radiator")bodyHeightRatio=.12;
                    else if(spec.Key=="Transformer_Fan")bodyHeightRatio=.22;
                    else if(spec.Key=="Transformer_BushingHV"||spec.Key=="Transformer_BushingMV"||spec.Key=="GIS_VoltageTransformer")bodyHeightRatio=.52;
                    else if(spec.Key=="GIS_Termination")bodyHeightRatio=.30;
                    else if(spec.Key=="BatteryRack")bodyHeightRatio=.10;
                    else if(spec.Key=="CableTrench")bodyHeightRatio=.12;
                    else if(spec.Key=="CableTray")bodyHeightRatio=.35;
                    else if(spec.Key=="GIS_BayFrame"||spec.Key=="SteelSupport")bodyHeightRatio=.08;
                    else if(spec.Key=="OilContainment")bodyHeightRatio=.10;
                    fm.Set(bodyHeight,M(spec.Height*bodyHeightRatio));
                    double nominalVoltageKv=GISNominalVoltageKv(spec.Key);
                    double voltageClassKv=GISVoltageClassKv(spec.Key,spec.VoltageKv);
                    fm.Set(nominalVoltage,nominalVoltageKv);
                    fm.Set(voltage,voltageClassKv);
                    fm.Set(voltageSecondary,spec.SecondaryVoltageKv);
                    fm.Set(voltageSecondaryClass,spec.Key=="Transformer_Tank"?17.5:0.0);
                    fm.Set(rating,spec.RatingMva);
                    double ratedCurrent=spec.CurrentA;
                    double currentVoltageKv=nominalVoltageKv>0?nominalVoltageKv:voltageClassKv;
                    if(ratedCurrent<=0&&spec.RatingMva>0&&currentVoltageKv>0)ratedCurrent=spec.RatingMva*1000000.0/(Math.Sqrt(3.0)*currentVoltageKv*1000.0);
                    fm.Set(current,ratedCurrent);
                    double secondaryCurrent=spec.SecondaryVoltageKv>0&&spec.RatingMva>0?spec.RatingMva*1000000.0/(Math.Sqrt(3.0)*spec.SecondaryVoltageKv*1000.0):0.0;
                    fm.Set(currentSecondary,secondaryCurrent);
                    fm.Set(impedance,spec.ImpedancePu);
                    fm.Set(geometryState,spec.GeometryStatus);
                    fm.Set(dataState,"PREMISSA / NAO SELECIONADO");
                    fm.Set(source,spec.Source);
                    fm.Set(description,spec.Description);
                    fm.Set(manufacturer,"NAO SELECIONADO");
                    fm.Set(reference,spec.Source);

                    ViewPlan plan= new FilteredElementCollector(fdoc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().FirstOrDefault(v=>!v.IsTemplate);
                    if(plan==null)throw new InvalidOperationException("Template de familia sem vista de planta utilizavel: "+template);
                    double w=M(spec.Width),d=M(spec.Depth),h=M(spec.Height),x0=-w/2.0,x1=w/2.0,y0=-d/2.0,y1=d/2.0;
                    ReferencePlane left=fdoc.FamilyCreate.NewReferencePlane(new XYZ(x0,y0-.75,0),new XYZ(x0,y1+.75,0),XYZ.BasisZ,plan);left.Name="BIM Left";
                    ReferencePlane right=fdoc.FamilyCreate.NewReferencePlane(new XYZ(x1,y0-.75,0),new XYZ(x1,y1+.75,0),XYZ.BasisZ,plan);right.Name="BIM Right";
                    ReferencePlane front=fdoc.FamilyCreate.NewReferencePlane(new XYZ(x0-.75,y0,0),new XYZ(x1+.75,y0,0),XYZ.BasisZ,plan);front.Name="BIM Front";
                    ReferencePlane back=fdoc.FamilyCreate.NewReferencePlane(new XYZ(x0-.75,y1,0),new XYZ(x1+.75,y1,0),XYZ.BasisZ,plan);back.Name="BIM Back";
                    CurveArray profile=new CurveArray();
                    profile.Append(Line.CreateBound(new XYZ(x0,y0,0),new XYZ(x1,y0,0)));
                    profile.Append(Line.CreateBound(new XYZ(x1,y0,0),new XYZ(x1,y1,0)));
                    profile.Append(Line.CreateBound(new XYZ(x1,y1,0),new XYZ(x0,y1,0)));
                    profile.Append(Line.CreateBound(new XYZ(x0,y1,0),new XYZ(x0,y0,0)));
                    CurveArrArray profiles=new CurveArrArray();profiles.Append(profile);
                    SketchPlane sketch=SketchPlane.Create(fdoc,Plane.CreateByNormalAndOrigin(XYZ.BasisZ,XYZ.Zero));
                    Extrusion solid=fdoc.FamilyCreate.NewExtrusion(true,profiles,sketch,h*bodyHeightRatio);
                    Parameter end=solid.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM);
                    if(end==null||!fm.CanElementParameterBeAssociated(end))throw new InvalidOperationException("A altura da extrusao nao pode ser associada ao parametro da familia.");
                    fm.AssociateElementParameterToFamilyParameter(end,bodyHeight);
                    AddGISFamilyDetails(fdoc,plan,spec,w,d,h);
                    fdoc.Regenerate();
                    GeometryElement geometry=solid.get_Geometry(new Options{ComputeReferences=true,IncludeNonVisibleObjects=true,DetailLevel=ViewDetailLevel.Fine});
                    Solid shape=geometry.Cast<GeometryObject>().OfType<Solid>().FirstOrDefault(s=>s.Faces.Size>0);
                    if(shape==null)throw new InvalidOperationException("Extrusao sem solido para familia "+spec.Key);
                    PlanarFace leftFace=FindGISPlanarFace(shape,XYZ.BasisX,x0);
                    PlanarFace rightFace=FindGISPlanarFace(shape,XYZ.BasisX,x1);
                    PlanarFace frontFace=FindGISPlanarFace(shape,XYZ.BasisY,y0);
                    PlanarFace backFace=FindGISPlanarFace(shape,XYZ.BasisY,y1);
                    fdoc.FamilyCreate.NewAlignment(plan,left.GetReference(),leftFace.Reference);
                    fdoc.FamilyCreate.NewAlignment(plan,right.GetReference(),rightFace.Reference);
                    fdoc.FamilyCreate.NewAlignment(plan,front.GetReference(),frontFace.Reference);
                    fdoc.FamilyCreate.NewAlignment(plan,back.GetReference(),backFace.Reference);
                    if(spec.Key=="AuxiliaryPanelAC"||spec.Key=="UPS_Cabinet")
                    {
                        ConnectorElement terminalA=ConnectorElement.CreateElectricalConnector(fdoc,Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerCircuit,leftFace.Reference);
                        SetGISConnectorStudyData(terminalA,.380,spec.Key,"Porta de entrada CA do sistema auxiliar QAC-01 a UPS-01; sistema logico; sem fio fisico 3D.");
                        Log("GIS_FAMILY_CONNECTOR_CREATED family="+spec.Key+" voltage_kV=0.380 purpose=AUX-UPS-01");
                    }
                    ReferenceArray widthRefs=new ReferenceArray();widthRefs.Append(left.GetReference());widthRefs.Append(right.GetReference());
                    ReferenceArray depthRefs=new ReferenceArray();depthRefs.Append(front.GetReference());depthRefs.Append(back.GetReference());
                    Dimension widthDim=fdoc.FamilyCreate.NewLinearDimension(plan,Line.CreateBound(new XYZ(x0,y0-.35,0),new XYZ(x1,y0-.35,0)),widthRefs);
                    Dimension depthDim=fdoc.FamilyCreate.NewLinearDimension(plan,Line.CreateBound(new XYZ(x0-.35,y0,0),new XYZ(x0-.35,y1,0)),depthRefs);
                    widthDim.FamilyLabel=width;
                    depthDim.FamilyLabel=depth;
                    fdoc.Regenerate();
                    tx.Commit();
                }
                SaveAsOptions save=new SaveAsOptions{OverwriteExistingFile=true};
                fdoc.SaveAs(path,save);
            }
        }

        private static double GISNominalVoltageKv(string familyKey)
        {
            if(familyKey.StartsWith("GIS_",StringComparison.OrdinalIgnoreCase))return 138.0;
            if(familyKey=="Transformer_Tank"||familyKey=="Transformer_BushingHV")return 138.0;
            if(familyKey=="Transformer_BushingMV"||familyKey.StartsWith("MV",StringComparison.OrdinalIgnoreCase))return 13.8;
            if(familyKey=="ProtectionPanel"||familyKey=="AuxiliaryPanelDC"||familyKey=="BatteryRack")return .125;
            if(familyKey=="AuxiliaryPanelAC"||familyKey=="UPS_Cabinet")return .380;
            return 0.0;
        }

        private static void SetGISConnectorStudyData(ConnectorElement connector,double nominalVoltageKv,string familyKey,string description)
        {
            Parameter voltage=connector.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE);
            if(voltage!=null&&!voltage.IsReadOnly&&nominalVoltageKv>0)
            {
                double volts=UnitUtils.ConvertToInternalUnits(nominalVoltageKv*1000.0,UnitTypeId.Volts);
                voltage.Set(volts);
            }
            Parameter poleParam=connector.get_Parameter(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES);
            int poleCount=(familyKey=="AuxiliaryPanelAC"||(familyKey=="UPS_Cabinet"&&nominalVoltageKv>0.2))?3:(nominalVoltageKv>=1.0?3:2);
            if(poleParam!=null&&!poleParam.IsReadOnly)poleParam.Set(poleCount);
            Parameter connectorDescription=connector.get_Parameter(BuiltInParameter.RBS_CONNECTOR_DESCRIPTION);
            if(connectorDescription!=null&&!connectorDescription.IsReadOnly)connectorDescription.Set(description);
        }

        private static double GISVoltageClassKv(string familyKey,double specifiedClassKv)
        {
            if(familyKey=="Transformer_Tank")return 145.0;
            if(familyKey=="Transformer_BushingMV")return 17.5;
            return specifiedClassKv;
        }

        private static string GISFamilyFileStem(GISFamilySpec spec)
        {
            return spec.Key.StartsWith("GIS_",StringComparison.OrdinalIgnoreCase)?spec.Key:"GIS_"+spec.Key;
        }

        private static void AddGISFamilyDetails(Document fdoc,ViewPlan plan,GISFamilySpec spec,double w,double d,double h)
        {
            double x0=-w/2.0,y0=-d/2.0;
            if(spec.Key=="Transformer_Tank")
            {
                AddGISFamilyBox(fdoc,x0+w*.06,y0+d*.04,w*.88,d*.92,h*.83,h*.08);
                AddGISFamilyBox(fdoc,x0+w*.08,y0-.10,w*.16,.10,h*.25,h*.58);
                AddGISFamilyBox(fdoc,x0+w*.74,y0-.10,w*.16,.10,h*.25,h*.58);
                AddGISFamilyBox(fdoc,x0+w*.18,y0+d*.80,w*.64,d*.16,h*.87,h*.07);
            }
            else if(spec.Key=="Transformer_Radiator")
            {
                for(int i=0;i<9;i++)
                {
                    double x=x0+w*(.08+i*.105);
                    AddGISFamilyBox(fdoc,x,y0-.035,w*.055,d+.07,0,h);
                }
                AddGISFamilyBox(fdoc,x0,y0-.11,w,.08,h*.10,h*.10);
                AddGISFamilyBox(fdoc,x0,y0-.11,w,.08,h*.80,h*.10);
            }
            else if(spec.Key=="Transformer_Fan")
            {
                AddGISFamilyBox(fdoc,x0+w*.12,y0-.08,w*.76,.08,h*.34,h*.18);
                for(int i=0;i<5;i++)AddGISFamilyBox(fdoc,x0+w*(.18+i*.14),y0-.10,w*.06,.10,h*.12,h*.68);
            }
            else if(spec.Key=="Transformer_BushingHV"||spec.Key=="Transformer_BushingMV"||spec.Key=="GIS_VoltageTransformer")
            {
                AddGISFamilyBox(fdoc,x0+w*.17,y0+d*.17,w*.66,d*.66,h*.10,h*.12);
                double radius=Math.Min(w,d)*.30;
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.19),XYZ.BasisZ,radius*.95,h*.16);
                for(int i=0;i<7;i++)
                {
                    double z=h*(.28+i*.075);
                    double skirt=radius*(1.18-(i%2)*.10);
                    AddGISFamilyCylinder(fdoc,new XYZ(0,0,z),XYZ.BasisZ,skirt,h*.035);
                }
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.31),XYZ.BasisZ,radius*.58,h*.45);
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.76),XYZ.BasisZ,radius*.36,h*.12);
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.88),XYZ.BasisZ,radius*.20,h*.09);
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.97),XYZ.BasisZ,radius*.10,h*.03);
            }
            else if(spec.Key=="GIS_CircuitBreaker")
            {
                AddGISFamilyBox(fdoc,x0+w*.08,y0-.08,w*.35,.08,h*.18,h*.55);
                AddGISFamilyBox(fdoc,x0+w*.57,y0-.08,w*.35,.08,h*.18,h*.55);
                AddGISFamilyBox(fdoc,x0+w*.20,y0-.12,w*.60,.12,h*.05,h*.12);
                AddGISFamilyBox(fdoc,x0+w*.38,y0-.12,w*.24,.12,h*.72,h*.13);
                for(int phase=0;phase<3;phase++)
                {
                    double y=y0+d*(.22+phase*.28);
                    AddGISFamilyTube(fdoc,new XYZ(x0+w*.16,y,h*.54),XYZ.BasisX,w*.70,Math.Min(d,h)*.095);
                    AddGISFamilyAnnulus(fdoc,new XYZ(x0+w*.27,y,h*.54),XYZ.BasisX,w*.045,Math.Min(d,h)*.145,Math.Min(d,h)*.095);
                    AddGISFamilyAnnulus(fdoc,new XYZ(x0+w*.66,y,h*.54),XYZ.BasisX,w*.045,Math.Min(d,h)*.145,Math.Min(d,h)*.095);
                }
            }
            else if(spec.Key=="GIS_Disconnector")
            {
                AddGISFamilyBox(fdoc,x0+w*.10,y0-.09,w*.80,.09,h*.12,h*.12);
                AddGISFamilyBox(fdoc,x0+w*.18,y0-.10,w*.10,.10,h*.24,h*.62);
                AddGISFamilyBox(fdoc,x0+w*.72,y0-.10,w*.10,.10,h*.24,h*.62);
                AddGISFamilyPolygonExtrusion(fdoc,new[]{new XYZ(x0+w*.24,y0-.13,h*.64),new XYZ(x0+w*.35,y0-.13,h*.64),new XYZ(x0+w*.78,y0+d*.18,h*.64),new XYZ(x0+w*.68,y0+d*.18,h*.64)},h*.08);
                AddGISFamilyBox(fdoc,x0+w*.36,y0-.14,w*.28,.14,h*.78,h*.15);
                for(int phase=0;phase<3;phase++)
                {
                    double y=y0+d*(.22+phase*.28);
                    AddGISFamilyTube(fdoc,new XYZ(x0+w*.16,y,h*.48),XYZ.BasisX,w*.68,Math.Min(d,h)*.055);
                    AddGISFamilyBox(fdoc,x0+w*.44,y-Math.Min(d,h)*.035,w*.28,Math.Min(d,h)*.07,h*.62,Math.Min(d,h)*.07);
                }
            }
            else if(spec.Key=="GIS_EarthingSwitch")
            {
                AddGISFamilyBox(fdoc,x0+w*.10,y0-.09,w*.80,.09,h*.12,h*.12);
                AddGISFamilyBox(fdoc,x0+w*.20,y0-.10,w*.12,.10,h*.24,h*.62);
                AddGISFamilyBox(fdoc,x0+w*.68,y0-.10,w*.12,.10,h*.24,h*.62);
                AddGISFamilyBox(fdoc,x0+w*.38,y0-.13,w*.12,.13,h*.48,h*.38);
                AddGISFamilyBox(fdoc,x0+w*.14,y0-.11,w*.72,.11,h*.12,h*.08);
                for(int phase=0;phase<3;phase++)
                {
                    double y=y0+d*(.22+phase*.28);
                    AddGISFamilyTube(fdoc,new XYZ(x0+w*.18,y,h*.42),XYZ.BasisX,w*.64,Math.Min(d,h)*.05);
                    AddGISFamilyBox(fdoc,x0+w*.44,y-Math.Min(d,h)*.035,w*.30,Math.Min(d,h)*.07,h*.14,h*.055);
                    AddGISFamilyBox(fdoc,x0+w*.29,y-Math.Min(d,h)*.025,w*.08,Math.Min(d,h)*.05,h*.19,h*.27);
                }
            }
            else if(spec.Key=="GIS_CurrentTransformer")
            {
                AddGISFamilyBox(fdoc,x0+w*.12,y0-.11,w*.76,.11,h*.15,h*.12);
                AddGISFamilyBox(fdoc,x0+w*.34,y0-.13,w*.32,.13,h*.28,h*.46);
                AddGISFamilyBox(fdoc,x0+w*.12,y0-.11,w*.76,.11,h*.77,h*.12);
                for(int phase=0;phase<3;phase++)
                {
                    double y=y0+d*(.22+phase*.28);
                    AddGISFamilyAnnulus(fdoc,new XYZ(x0+w*.43,y,h*.52),XYZ.BasisX,w*.14,Math.Min(d,h)*.24,Math.Min(d,h)*.14);
                    AddGISFamilyTube(fdoc,new XYZ(x0+w*.17,y,h*.52),XYZ.BasisX,w*.66,Math.Min(d,h)*.07);
                }
            }
            else if(spec.Key=="GIS_BusModule"||spec.Key=="MVBusModule")
            {
                double radius=Math.Min(d,h)*.36;
                double centerY=y0+d*.50,centerZ=h*.54;
                AddGISFamilyTube(fdoc,new XYZ(x0,centerY,centerZ),XYZ.BasisX,w,radius);
                AddGISFamilyAnnulus(fdoc,new XYZ(x0+w*.05,centerY,centerZ),XYZ.BasisX,w*.055,radius*1.32,radius*.83);
                AddGISFamilyAnnulus(fdoc,new XYZ(x0+w*.895,centerY,centerZ),XYZ.BasisX,w*.055,radius*1.32,radius*.83);
            }
            else if(spec.Key=="GIS_Termination")
            {
                AddGISFamilyBox(fdoc,x0+w*.15,y0+d*.15,w*.70,d*.70,h*.10,h*.14);
                double radius=Math.Min(w,d)*.34;
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.24),XYZ.BasisZ,radius,h*.19);
                for(int i=0;i<5;i++)AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*(.38+i*.105)),XYZ.BasisZ,radius*(1.0-i*.08),h*.045);
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.86),XYZ.BasisZ,radius*.48,h*.12);
                AddGISFamilyCylinder(fdoc,new XYZ(0,0,h*.98),XYZ.BasisZ,radius*.26,h*.02);
                AddGISFamilyBox(fdoc,x0+w*.30,y0+d*.30,w*.40,d*.40,h*.89,h*.07);
            }
            else if(spec.Key=="MV_Cubicle"||spec.Key=="MV_BusCoupler")
            {
                AddGISFamilyBox(fdoc,x0+w*.08,y0-.045,w*.84,.045,h*.10,h*.80);
                for(int i=0;i<3;i++)AddGISFamilyBox(fdoc,x0+w*.14,y0-.06,w*.52,.06,h*(.18+i*.22),h*.045);
                AddGISFamilyBox(fdoc,x0+w*.73,y0-.065,w*.08,.065,h*.40,h*.08);
                AddGISFamilyBox(fdoc,x0+w*.78,y0-.07,w*.12,.07,h*.74,h*.10);
            }
            else if(spec.Key=="ProtectionPanel"||spec.Key=="AuxiliaryPanelAC"||spec.Key=="AuxiliaryPanelDC"||spec.Key=="UPS_Cabinet")
            {
                AddGISFamilyBox(fdoc,x0+w*.08,y0-.04,w*.84,.04,h*.06,h*.88);
                AddGISFamilyBox(fdoc,x0+w*.18,y0-.06,w*.45,.06,h*.57,h*.20);
                for(int i=0;i<3;i++)AddGISFamilyBox(fdoc,x0+w*.18,y0-.055,w*.45,.055,h*(.18+i*.11),h*.035);
                AddGISFamilyBox(fdoc,x0+w*.73,y0-.055,w*.12,.055,h*.35,h*.12);
            }
            else if(spec.Key=="BatteryRack")
            {
                for(int row=0;row<2;row++)for(int col=0;col<6;col++)
                    AddGISFamilyBox(fdoc,x0+w*(.06+col*.15),y0-.18+row*d*.43,w*.12,d*.32,h*.10,h*.80);
                AddGISFamilyBox(fdoc,x0+w*.02,y0-.10,w*.96,d*.90,h*.05,h*.06);
                AddGISFamilyBox(fdoc,x0+w*.02,y0-.10,w*.96,d*.90,h*.88,h*.06);
            }
            else if(spec.Key=="CableTrench")
            {
                AddGISFamilyBox(fdoc,x0,y0,w*.12,d,h*.08,h*.84);
                AddGISFamilyBox(fdoc,x0+w*.88,y0,w*.12,d,h*.08,h*.84);
                AddGISFamilyBox(fdoc,x0+w*.12,y0,w*.76,d*.12,h*.08,h*.82);
                AddGISFamilyBox(fdoc,x0+w*.12,y0+d*.88,w*.76,d*.12,h*.08,h*.82);
            }
            else if(spec.Key=="CableTray")
            {
                AddGISFamilyBox(fdoc,x0,y0,w,d*.12,0,h);
                AddGISFamilyBox(fdoc,x0,y0+d*.88,w,d*.12,0,h);
                for(int i=1;i<7;i++)AddGISFamilyBox(fdoc,x0+w*(i/7.0),y0+d*.12,w*.035,d*.76,h*.35,h*.30);
            }
            else if(spec.Key=="OilContainment")
            {
                AddGISFamilyBox(fdoc,x0,y0,w,d*.08,h*.10,h*.82);
                AddGISFamilyBox(fdoc,x0,y0+d*.92,w,d*.08,h*.10,h*.82);
                AddGISFamilyBox(fdoc,x0,y0+d*.08,w*.08,d*.84,h*.10,h*.82);
                AddGISFamilyBox(fdoc,x0+w*.92,y0+d*.08,w*.08,d*.84,h*.10,h*.82);
                AddGISFamilyBox(fdoc,x0+w*.44,y0+d*.44,w*.12,d*.12,h*.10,h*.06);
            }
            else if(spec.Key=="GIS_BayFrame"||spec.Key=="SteelSupport")
            {
                for(int i=0;i<4;i++)
                {
                    double x=x0+(i%2==0?w*.08:w*.82),y=y0+(i<2?d*.08:d*.82);
                    AddGISFamilyBox(fdoc,x,y,w*.10,d*.10,0,h*.82);
                }
                AddGISFamilyBox(fdoc,x0+w*.08,y0+d*.82,w*.84,d*.10,h*.76,h*.08);
                AddGISFamilyBox(fdoc,x0+w*.08,y0+d*.08,w*.84,d*.10,h*.76,h*.08);
            }
            else if(spec.Key.StartsWith("BuildingRoof_",StringComparison.OrdinalIgnoreCase))
            {
                double plate=h*.52,ribHeight=h*.34,ribWidth=Math.Min(w,d)*.018;
                AddGISFamilyBox(fdoc,x0,y0,w,d,h*.08,plate);
                for(int i=0;i<7;i++)
                {
                    double x=x0+w*(.08+i*.14);
                    AddGISFamilyBox(fdoc,x-ribWidth/2,y0+d*.04,ribWidth,d*.92,h*.08+plate,ribHeight);
                }
                AddGISFamilyBox(fdoc,x0,y0,w,d*.035,h*.08+plate,ribHeight);
                AddGISFamilyBox(fdoc,x0,y0+d*.965,w,d*.035,h*.08+plate,ribHeight);
            }
        }

        private static void AddGISFamilyTube(Document fdoc,XYZ startCenter,XYZ axis,double length,double radius)
        {
            if(length<=0||radius<=0)throw new ArgumentOutOfRangeException("Dimensoes de tubo GIS devem ser positivas.");
            XYZ normal=axis.Normalize();
            XYZ xAxis=Math.Abs(normal.DotProduct(XYZ.BasisZ))>.95?XYZ.BasisX:XYZ.BasisZ.CrossProduct(normal).Normalize();
            XYZ yAxis=normal.CrossProduct(xAxis).Normalize();
            CurveArray loop=new CurveArray();
            loop.Append(Arc.Create(startCenter,radius,0,2*Math.PI,xAxis,yAxis));
            CurveArrArray profile=new CurveArrArray();profile.Append(loop);
            SketchPlane sketch=SketchPlane.Create(fdoc,Plane.CreateByNormalAndOrigin(normal,startCenter));
            fdoc.FamilyCreate.NewExtrusion(true,profile,sketch,length);
        }

        private static void AddGISFamilyCylinder(Document fdoc,XYZ startCenter,XYZ axis,double radius,double length)
        {
            AddGISFamilyTube(fdoc,startCenter,axis,length,radius);
        }

        private static void AddGISFamilyAnnulus(Document fdoc,XYZ startCenter,XYZ axis,double length,double outerRadius,double innerRadius)
        {
            if(length<=0||innerRadius<=0||outerRadius<=innerRadius)throw new ArgumentOutOfRangeException("Dimensoes do anel GIS devem ser positivas e concentricas.");
            XYZ normal=axis.Normalize();
            XYZ xAxis=Math.Abs(normal.DotProduct(XYZ.BasisZ))>.95?XYZ.BasisX:XYZ.BasisZ.CrossProduct(normal).Normalize();
            XYZ yAxis=normal.CrossProduct(xAxis).Normalize();
            CurveArray outer=new CurveArray();outer.Append(Arc.Create(startCenter,outerRadius,0,2*Math.PI,xAxis,yAxis));
            CurveArray inner=new CurveArray();inner.Append(Arc.Create(startCenter,innerRadius,0,2*Math.PI,xAxis,yAxis));
            CurveArrArray profile=new CurveArrArray();profile.Append(outer);profile.Append(inner);
            SketchPlane sketch=SketchPlane.Create(fdoc,Plane.CreateByNormalAndOrigin(normal,startCenter));
            fdoc.FamilyCreate.NewExtrusion(true,profile,sketch,length);
        }

        private static void AddGISFamilyBox(Document fdoc,double x,double y,double w,double d,double z,double h)
        {
            CurveArray profile=new CurveArray();
            profile.Append(Line.CreateBound(new XYZ(x,y,z),new XYZ(x+w,y,z)));
            profile.Append(Line.CreateBound(new XYZ(x+w,y,z),new XYZ(x+w,y+d,z)));
            profile.Append(Line.CreateBound(new XYZ(x+w,y+d,z),new XYZ(x,y+d,z)));
            profile.Append(Line.CreateBound(new XYZ(x,y+d,z),new XYZ(x,y,z)));
            CurveArrArray profiles=new CurveArrArray();profiles.Append(profile);
            SketchPlane sketch=SketchPlane.Create(fdoc,Plane.CreateByNormalAndOrigin(XYZ.BasisZ,new XYZ(0,0,z)));
            fdoc.FamilyCreate.NewExtrusion(true,profiles,sketch,h);
        }

        private static void AddGISFamilyPolygonExtrusion(Document fdoc,IList<XYZ> points,double h)
        {
            CurveArray profile=new CurveArray();
            for(int i=0;i<points.Count;i++)profile.Append(Line.CreateBound(points[i],points[(i+1)%points.Count]));
            CurveArrArray profiles=new CurveArrArray();profiles.Append(profile);
            SketchPlane sketch=SketchPlane.Create(fdoc,Plane.CreateByNormalAndOrigin(XYZ.BasisZ,new XYZ(0,0,points[0].Z)));
            fdoc.FamilyCreate.NewExtrusion(true,profiles,sketch,h);
        }

        private static PlanarFace FindGISPlanarFace(Solid solid,XYZ axis,double coordinate)
        {
            foreach(Face face in solid.Faces)
            {
                PlanarFace planar=face as PlanarFace;
                if(planar==null)continue;
                double normal=Math.Abs(planar.FaceNormal.DotProduct(axis));
                double value=axis.X!=0?planar.Origin.X:(axis.Y!=0?planar.Origin.Y:planar.Origin.Z);
                if(normal>.999&&Math.Abs(value-coordinate)<1e-5)return planar;
            }
            throw new InvalidOperationException("Face plana parametrica nao encontrada em "+coordinate.ToString("R"));
        }

        private static Dictionary<string,FamilySymbol> LoadGISFamilySymbols(Document doc,string outDir)
        {
            var symbols=new Dictionary<string,FamilySymbol>(StringComparer.OrdinalIgnoreCase);
            foreach(GISFamilySpec spec in GetGISFamilySpecs())
            {
                string familyStem=GISFamilyFileStem(spec);
                string path=Path.Combine(outDir,"familias","reconstrucao-r9",familyStem+".rfa");
                if(!File.Exists(path))throw new FileNotFoundException("Familia GIS esperada nao foi gerada",path);
                Family family=null;
                try{doc.LoadFamily(path,out family);}catch(Exception ex){Log("GIS_FAMILY_LOAD_NOTE "+spec.Key+" "+ex.Message);}
                if(family==null)family=new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(f=>f.Name.StartsWith(familyStem,StringComparison.OrdinalIgnoreCase));
                if(family==null)throw new InvalidOperationException("Revit nao carregou a familia GIS "+spec.Key);
                FamilySymbol symbol=family.GetFamilySymbolIds().Select(id=>doc.GetElement(id) as FamilySymbol).FirstOrDefault(x=>x!=null&&x.Name==spec.TypeName);
                if(symbol==null)throw new InvalidOperationException("Tipo da familia GIS ausente: "+spec.Key+" / "+spec.TypeName);
                if(!symbol.IsActive)symbol.Activate();
                symbols.Add(spec.Key,symbol);
            }
            return symbols;
        }

        private static FamilyInstance AddGISFamilyInstance(Document doc,Dictionary<string,FamilySymbol> symbols,string familyKey,string mark,string comments,double x,double y,double z,double rotationDegrees=0)
        {
            FamilySymbol symbol=symbols[familyKey];
            FamilyInstance instance=doc.Create.NewFamilyInstance(new XYZ(M(x),M(y),M(z)),symbol,Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            Parameter tag=instance.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);if(tag!=null&&!tag.IsReadOnly)tag.Set(mark);
            Parameter note=instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);if(note!=null&&!note.IsReadOnly)note.Set(comments);
            string system=familyKey.StartsWith("MV",StringComparison.OrdinalIgnoreCase)?"DISTRIBUICAO_13K8":(familyKey.StartsWith("Transformer",StringComparison.OrdinalIgnoreCase)||familyKey=="OilContainment"?"TRANSFORMACAO_138_13K8":((familyKey.StartsWith("Protection",StringComparison.OrdinalIgnoreCase)||familyKey.StartsWith("Auxiliary",StringComparison.OrdinalIgnoreCase)||familyKey.StartsWith("UPS",StringComparison.OrdinalIgnoreCase)||familyKey.StartsWith("Battery",StringComparison.OrdinalIgnoreCase))?"CONTROLE_SERVICOS_AUXILIARES":"GIS_145K"));
            Parameter systemParam=instance.LookupParameter("BIM_Sistema_Elemento");if(systemParam!=null&&!systemParam.IsReadOnly)systemParam.Set(system);
            Parameter locationParam=instance.LookupParameter("BIM_Localizacao");if(locationParam!=null&&!locationParam.IsReadOnly)locationParam.Set(string.Format(System.Globalization.CultureInfo.InvariantCulture,"X={0:0.000} m; Y={1:0.000} m; Z={2:0.000} m",x,y,z));
            string[] markParts=mark.Split('-');
            string bay=markParts.Length>=2?markParts[0]+"-"+markParts[1]:mark;
            Parameter bayParam=instance.LookupParameter("BIM_Bay");if(bayParam!=null&&!bayParam.IsReadOnly)bayParam.Set(bay);
            if(Math.Abs(rotationDegrees)>1e-6)
            {
                XYZ origin=new XYZ(M(x),M(y),M(z));
                Line axis=Line.CreateBound(origin,origin+XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc,instance.Id,axis,rotationDegrees*Math.PI/180.0);
            }
            return instance;
        }

        private static void TryCreateGISAuxCircuit(Document doc,FamilyInstance panel,FamilyInstance load)
        {
            using(SubTransaction attempt=new SubTransaction(doc))
            {
                attempt.Start();
                try
                {
                    Autodesk.Revit.DB.Electrical.ElectricalSetting settings=Autodesk.Revit.DB.Electrical.ElectricalSetting.GetElectricalSettings(doc);
                    Autodesk.Revit.DB.Electrical.VoltageType v380=settings.AddVoltageType("GIS 380 V linha-linha - estudo",380,370,390);
                    Autodesk.Revit.DB.Electrical.VoltageType v220=settings.AddVoltageType("GIS 220 V linha-terra - estudo",220,210,230);
                    Autodesk.Revit.DB.Electrical.DistributionSysType distribution=settings.AddDistributionSysType(
                        "GIS 380Y-220 V trifasico - estudo",Autodesk.Revit.DB.Electrical.ElectricalPhase.ThreePhase,
                        Autodesk.Revit.DB.Electrical.ElectricalPhaseConfiguration.Wye,4,v380,v220);
                    Autodesk.Revit.DB.Electrical.ElectricalEquipment panelEquipment=panel.MEPModel as Autodesk.Revit.DB.Electrical.ElectricalEquipment;
                    if(panelEquipment==null)throw new InvalidOperationException("QAC nao esta tipado como equipamento eletrico selecionavel como painel.");
                    panelEquipment.DistributionSystem=distribution;
                    Connector loadConnector=load.MEPModel==null?null:load.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                        .Where(c=>c.Domain==Domain.DomainElectrical&&c.ElectricalSystemType==Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerCircuit)
                        .OrderBy(c=>c.Origin.X).FirstOrDefault();
                    if(loadConnector==null)throw new InvalidOperationException("UPS nao expoe um conector eletrico PowerCircuit utilizavel.");
                    Autodesk.Revit.DB.Electrical.ElectricalSystem circuit=Autodesk.Revit.DB.Electrical.ElectricalSystem.Create(
                        loadConnector,Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerCircuit);
                    if(circuit==null)throw new InvalidOperationException("Revit nao criou sistema eletrico a partir do conector do UPS.");
                    circuit.SelectPanel(panel);
                    doc.Regenerate();
                    if(attempt.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("Subtransacao do circuito auxiliar nao foi confirmada.");
                    Log("GIS_AUXILIARY_CIRCUIT_CREATED id="+circuit.Id.Value+" panel="+panel.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString()+" load="+load.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString());
                }
                catch(Exception ex)
                {
                    if(attempt.GetStatus()==TransactionStatus.Started)attempt.RollBack();
                    Log("GIS_AUXILIARY_CIRCUIT_FAILED "+ex.GetType().Name+" "+ex.Message);
                }
            }
        }

        private static void TryCreateGISAuxWire(Document doc,ViewPlan view)
        {
            using(SubTransaction attempt=new SubTransaction(doc))
            {
                attempt.Start();
                try
                {
                    FamilyInstance panel=new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().First(x=>GISModelMark(x)=="QAC-01");
                    FamilyInstance load=new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().First(x=>GISModelMark(x)=="UPS-01");
                    Connector panelConnector=GetGISConnectors(panel)?.Cast<Connector>().FirstOrDefault(x=>x.Domain==Domain.DomainElectrical&&x.ElectricalSystemType==Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerCircuit);
                    Connector loadConnector=GetGISConnectors(load)?.Cast<Connector>().FirstOrDefault(x=>x.Domain==Domain.DomainElectrical&&x.ElectricalSystemType==Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerCircuit);
                    if(panelConnector==null||loadConnector==null)throw new InvalidOperationException("QAC-01 e UPS-01 precisam expor conectores eletricos PowerCircuit compativeis.");
                    Autodesk.Revit.DB.Electrical.WireType wireType=new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.WireType)).Cast<Autodesk.Revit.DB.Electrical.WireType>().FirstOrDefault();
                    if(wireType==null)throw new InvalidOperationException("O template nao contem tipo nativo Wire para o plano auxiliar.");
                    double routeY=Math.Max(panelConnector.Origin.Y,loadConnector.Origin.Y)+M(1.2);
                    double planeZ=view.GenLevel==null?panelConnector.Origin.Z:view.GenLevel.Elevation;
                    IList<XYZ> vertices=new List<XYZ>{new XYZ(panelConnector.Origin.X,routeY,planeZ),new XYZ(loadConnector.Origin.X,routeY,planeZ)};
                    Autodesk.Revit.DB.Electrical.Wire wire=Autodesk.Revit.DB.Electrical.Wire.Create(doc,wireType.Id,view.Id,Autodesk.Revit.DB.Electrical.WiringType.Chamfer,vertices,panelConnector,loadConnector);
                    Parameter mark=wire.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);if(mark!=null&&!mark.IsReadOnly)mark.Set("AUX-UPS-01-WIRE");
                    Parameter comments=wire.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);if(comments!=null&&!comments.IsReadOnly)comments.Set("Fio eletrico nativo Revit 2D, visivel somente no plano E-06 e conectado aos conectores QAC-01/UPS-01. Representacao grafica de circuito auxiliar; nao e cabo 3D, nao dimensiona condutor e nao prova origem/alimentacao completa.");
                    doc.Regenerate();
                    ConnectorSet wireConnectors=wire.ConnectorManager.Connectors;int connected=wireConnectors.Cast<Connector>().Count(x=>x.IsConnected);
                    if(connected<2)throw new InvalidOperationException("O fio foi criado, mas Revit nao confirmou ambos os conectores de extremidade. connected="+connected.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if(attempt.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("Subtransacao do fio auxiliar nao foi confirmada.");
                    Log("GIS_AUXILIARY_2D_WIRE_CREATED id="+wire.Id.Value+" view="+view.Name+" connectors="+wireConnectors.Size+" connected="+connected+" 3d_cable=false");
                }
                catch(Exception ex)
                {
                    if(attempt.GetStatus()==TransactionStatus.Started)attempt.RollBack();
                    Log("GIS_AUXILIARY_2D_WIRE_FAILED "+ex.GetType().Name+" "+ex.Message);
                }
            }
        }

        private static void DrawGISSingleLine(Document doc,ViewDrafting view,ElementId text,GraphicsStyle line,GraphicsStyle power)
        {
            TextNote.Create(doc,view.Id,XY(.45,10.35,0),"E-01 | UNIFILAR DE POTENCIA | 138 kV / 13,8 kV | 60 Hz | R9",text);
            TextNote.Create(doc,view.Id,XY(.45,9.95,0),"Topologia de estudo: dupla barra AT com acoplador; selecao de barra por bay; dois transformadores 40 MVA; MT em secoes A/B.",text);
            const double busA=9.10,busB=7.85;
            DrawLine(doc,view,1.55,busA,16.95,busA,line,0);
            DrawLine(doc,view,1.55,busB,16.95,busB,power,0);
            TextNote.Create(doc,view.Id,XY(.50,busA+.06,0),"BARRA A\n138 kV",text);
            TextNote.Create(doc,view.Id,XY(.50,busB+.06,0),"BARRA B\n138 kV",text);
            double[] x={2.25,5.65,9.10,12.55,15.95};
            string[] tags={"GIS-L01","GIS-T01","GIS-BC01","GIS-T02","GIS-L02"};
            string[] labels={"ENTRADA L01","TR-01","ACOPLADOR","TR-02","ENTRADA L02"};
            for(int i=0;i<x.Length;i++)
            {
                double cx=x[i];
                if(i==2)
                {
                    DrawLine(doc,view,cx,busA,cx,8.88,line,0);DrawGISDisconnectorSymbol(doc,view,cx,8.72,"89A",text,line);
                    DrawLine(doc,view,cx,8.56,cx,8.38,line,0);DrawGISBreakerSymbol(doc,view,cx,8.30,"52-BC",text,power);
                    DrawLine(doc,view,cx,8.08,cx,8.02,line,0);DrawGISDisconnectorSymbol(doc,view,cx,7.97,"89B",text,line);
                    DrawLine(doc,view,cx,7.81,cx,busB,line,0);
                    TextNote.Create(doc,view.Id,XY(cx-.55,7.26,0),"Acoplador\n52-BC normal ABERTO*",text);
                    continue;
                }
                // Two separate bus-selector branches converge only through the selected 89A or 89B switch.
                DrawLine(doc,view,cx-.18,busA,cx-.18,8.80,line,0);
                DrawGISDisconnectorSymbol(doc,view,cx-.18,8.62,(i==3||i==4)?"89A O":"89A C",text,line);
                DrawLine(doc,view,cx-.18,8.46,cx-.18,7.00,line,0);
                DrawLine(doc,view,cx-.18,7.00,cx,6.82,line,0);
                DrawLine(doc,view,cx+.18,busB,cx+.18,7.60,power,0);
                DrawGISDisconnectorSymbol(doc,view,cx+.18,7.44,(i==3||i==4)?"89B C":"89B O",text,power);
                DrawLine(doc,view,cx+.18,7.28,cx+.18,7.00,power,0);
                DrawLine(doc,view,cx+.18,7.00,cx,6.82,power,0);
                DrawLine(doc,view,cx,6.82,cx,6.56,power,0);
                DrawGISBreakerSymbol(doc,view,cx,6.42,"52",text,power);
                DrawLine(doc,view,cx,6.20,cx,5.88,power,0);
                DrawGISCTSymbol(doc,view,cx,5.78,text,power);
                DrawLine(doc,view,cx,5.60,cx,5.12,power,0);
                TextNote.Create(doc,view.Id,XY(cx-.64,4.87,0),tags[i]+"\n"+labels[i],text);
                if(i==1||i==3)
                {
                    double bx=cx;
                    Circle(doc,view,bx-.10,4.25,.20,power,0);Circle(doc,view,bx+.10,4.25,.20,power,0);
                    TextNote.Create(doc,view.Id,XY(bx-.68,3.80,0),(i==1?"TR-01":"TR-02")+" | 40 MVA\n138/13,8 kV | Dyn1*",text);
                    DrawLine(doc,view,bx,3.80,bx,3.32,power,0);
                    DrawGISBreakerSymbol(doc,view,bx,3.20,i==1?"QMT-A-IN":"QMT-B-IN",text,power);
                    DrawLine(doc,view,bx,2.98,bx,2.52,power,0);
                }
                else
                {
                    DrawGISLineTerminal(doc,view,cx,4.62,text,power);
                    TextNote.Create(doc,view.Id,XY(cx-.55,4.12,0),i==0?"REDE L01\n138 kV":"REDE L02\n138 kV",text);
                }
            }
            DrawLine(doc,view,1.30,2.48,8.25,2.48,power,0);
            DrawLine(doc,view,9.95,2.48,16.90,2.48,power,0);
            TextNote.Create(doc,view.Id,XY(1.35,2.55,0),"BARRA MT A | 13,8 kV",text);
            TextNote.Create(doc,view.Id,XY(10.0,2.55,0),"BARRA MT B | 13,8 kV",text);
            DrawGISFeederBank(doc,view,new[]{1.75,2.68,3.61,4.54,5.47,6.40,7.33},new[]{"A-IN","A-MED","A-F01","A-F02","A-F03","A-F04","A-F05"},text,power);
            DrawGISFeederBank(doc,view,new[]{10.87,11.80,12.73,13.66,14.59,15.52,16.45},new[]{"B-F01","B-F02","B-F03","B-F04","B-F05","B-MED","B-IN"},text,power);
            DrawLine(doc,view,8.25,2.48,8.70,2.48,power,0);DrawLine(doc,view,9.50,2.48,9.95,2.48,power,0);
            DrawLine(doc,view,8.70,2.48,8.70,1.92,power,0);DrawLine(doc,view,9.50,2.48,9.50,1.92,power,0);
            DrawGISDisconnectorSymbol(doc,view,8.70,1.82,"89T-A",text,power);DrawGISDisconnectorSymbol(doc,view,9.50,1.82,"89T-B",text,power);
            DrawLine(doc,view,8.70,1.66,9.50,1.66,power,0);DrawGISBreakerSymbol(doc,view,9.10,1.48,"52-TIE",text,power);
            TextNote.Create(doc,view.Id,XY(8.20,1.10,0),"NORMAL ABERTO*",text);
            TextNote.Create(doc,view.Id,XY(.55,.40,0),"C=fechada; O=aberta. Estado normal de estudo: L01/TR-01 selecionam A; L02/TR-02 selecionam B; 89A/89B do acoplador fechadas e 52-BC aberto; 52-TIE aberto.",text);
            TextNote.Create(doc,view.Id,XY(.55,.08,0),"* Catálogo DAX TRP-EA40-DML-01: 30/40 MVA, Dyn1, Z=11%, não selecionado; o estudo R8 mantém Z=10% provisória. Proteções sem ajustes/seletividade.",text);
        }

        private static void DrawGISDisconnectorSymbol(Document doc,View view,double x,double y,string label,ElementId text,GraphicsStyle style)
        {
            DrawLine(doc,view,x,y-.11,x,y-.055,style,0);DrawLine(doc,view,x,y+.055,x,y+.11,style,0);
            Circle(doc,view,x,y-.055,.025,style,0);Circle(doc,view,x,y+.055,.025,style,0);
            DrawLine(doc,view,x-.035,y-.035,x+.045,y+.045,style,0);
            TextNote.Create(doc,view.Id,XY(x+.10,y-.04,0),label,text);
        }

        private static void DrawGISBreakerSymbol(Document doc,View view,double x,double y,string label,ElementId text,GraphicsStyle style)
        {
            DrawLine(doc,view,x,y+.18,x,y+.10,style,0);DrawLine(doc,view,x,y-.10,x,y-.18,style,0);
            Box(doc,view,x-.16,y-.10,x+.16,y+.10,"",text,style,0);
            TextNote.Create(doc,view.Id,XY(x+.22,y-.04,0),label,text);
        }

        private static void DrawGISCTSymbol(Document doc,View view,double x,double y,ElementId text,GraphicsStyle style)
        {
            Circle(doc,view,x,y,.12,style,0);TextNote.Create(doc,view.Id,XY(x+.17,y-.04,0),"TC",text);
        }

        private static void DrawGISLineTerminal(Document doc,View view,double x,double y,ElementId text,GraphicsStyle style)
        {
            DrawLine(doc,view,x,y+.18,x,y-.07,style,0);DrawLine(doc,view,x-.14,y-.08,x+.14,y-.08,style,0);
            DrawLine(doc,view,x-.10,y-.13,x+.10,y-.13,style,0);DrawLine(doc,view,x-.05,y-.18,x+.05,y-.18,style,0);
        }

        private static void DrawGISFeederBank(Document doc,View view,double[] x,string[] labels,ElementId text,GraphicsStyle style)
        {
            for(int i=0;i<x.Length;i++)
            {
                DrawLine(doc,view,x[i],2.48,x[i],2.18,style,0);DrawGISBreakerSymbol(doc,view,x[i],2.02,"52",text,style);
                DrawLine(doc,view,x[i],1.84,x[i],1.56,style,0);Box(doc,view,x[i]-.39,1.18,x[i]+.39,1.55,labels[i],text,style,0);
            }
        }

        private static void DrawGISProtectionArchitecture(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power,GraphicsStyle data)
        {
            TextNote.Create(doc,view.Id,XY(.35,10.0,0),"PROTECAO / MEDICAO / COMANDO / TELECOM | ARQUITETURA FUNCIONAL",text);
            Box(doc,view,.4,7.45,5.4,8.95,"LINHAS L01 / L02\n21 | 25 | 50/51 | 50N/51N\n50BF | 79 (se aplicavel)",text,power,0);
            Box(doc,view,6.15,7.45,11.8,8.95,"BARRAS 138 kV\n87B | 50BF | 86\nTCs / supervisao",text,power,0);
            Box(doc,view,12.55,7.45,17.9,8.95,"TR-01 / TR-02\n87T | 64REF | 63\n49 | 50/51 | 86",text,power,0);
            DrawLine(doc,view,5.4,8.2,6.15,8.2,data,0);DrawLine(doc,view,11.8,8.2,12.55,8.2,data,0);
            Box(doc,view,.4,4.65,5.4,6.0,"TC / TP / TCs de neutro\nMedicao / sincronismo\nqualidade de dados pendente",text,note,0);
            Box(doc,view,6.15,4.65,11.8,6.0,"IEDs por bay\nTRIP / CLOSE / 86\nintertravamentos a definir",text,power,0);
            Box(doc,view,12.55,4.65,17.9,6.0,"REDE DE ESTACAO\nFIBRA / SWITCHES\nprotocolo e VLAN pendentes",text,data,0);
            DrawLine(doc,view,2.9,7.45,2.9,6.0,note,0);DrawLine(doc,view,5.4,5.3,6.15,5.3,power,0);DrawLine(doc,view,11.8,5.3,12.55,5.3,data,0);
            Box(doc,view,.4,1.75,5.4,3.15,"HMI / SCADA\nGateway / historico\ntelecomando condicionado",text,data,0);
            Box(doc,view,6.15,1.75,11.8,3.15,"SINCRONISMO / SOE\nGPS/PTP conceito\nsem engenharia de tempo",text,data,0);
            Box(doc,view,12.55,1.75,17.9,3.15,"AUX CC / UPS\nenergia e autonomia\npendentes",text,note,0);
            DrawLine(doc,view,14.9,4.65,14.9,3.15,power,0);DrawLine(doc,view,5.4,2.45,6.15,2.45,data,0);DrawLine(doc,view,11.8,2.45,12.55,2.45,data,0);
            TextNote.Create(doc,view.Id,XY(.45,.75,0),"Funcoes ANSI sao candidatas. TCs/TPs, redundancia, seletividade, lógicas, mensagens e testes dependem dos estudos e fornecedores; IEC 61850 não foi implementada.",text);
        }

        private static void DrawGISCalculationSummary(Document doc,ViewDrafting view,ElementId text,GraphicsStyle note,GraphicsStyle power,GraphicsStyle data)
        {
            DrawGISReconstructionCalculationTable(doc,view,text,note,power,data);
        }

        private static string GisValue(string group,string key,string format)
        {
            string id=group=="normal_load"?"BP-01":(group=="n_minus_one"?"BP-08":(group=="power_factor_correction"?"BP-14":"BP-13"));
            return GisCalc(id,format);
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
            string readmePath=Path.Combine(outDir,"README.md");
            if(!File.Exists(readmePath))File.WriteAllText(readmePath,readme,new UTF8Encoding(false));
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
            List<FamilyInstance> elements=new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(x=>x.Category!=null&&(x.Category.Id==new ElementId(BuiltInCategory.OST_ElectricalEquipment)||x.Category.Id==new ElementId(BuiltInCategory.OST_GenericModel)))
                .OrderBy(x=>x.Category.Name).ThenBy(x=>x.Symbol.Family.Name).ThenBy(x=>x.Id.Value).ToList();
            string resultDir=Path.Combine(outDir,"dados","reconstrucao-r9");Directory.CreateDirectory(resultDir);
            string path=Path.Combine(resultDir,"quantitativos-modelo.csv");
            using(StreamWriter writer=new StreamWriter(path,false,new UTF8Encoding(true)))
            {
                writer.WriteLine("element_id;categoria_revit;familia;tipo;marca;mep_connectors;isconnected_true;allrefs_externos;tipos_conector;dominios;sistema;bay;localizacao_m;U_nominal_kV;U_classe_kV;U_sec_nominal_kV;U_sec_classe_kV;I_nominal_A;I_sec_ref_A;S_MVA;Z_pu;estado_geometria;fabricante;referencia;comentarios");
                foreach(FamilyInstance element in elements)
                {
                    string mark=element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()??"";
                    string comments=element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()??"";
                    LocationPoint point=element.Location as LocationPoint;
                    string location=point==null?"":string.Format(System.Globalization.CultureInfo.InvariantCulture,"X={0:0.000} m; Y={1:0.000} m; Z={2:0.000} m",UnitUtils.ConvertFromInternalUnits(point.Point.X,UnitTypeId.Meters),UnitUtils.ConvertFromInternalUnits(point.Point.Y,UnitTypeId.Meters),UnitUtils.ConvertFromInternalUnits(point.Point.Z,UnitTypeId.Meters));
                    int rowConnectorCount=0,rowConnectedCount=0,rowExternalRefs=0;List<string> connectorTypes=new List<string>(),domains=new List<string>();
                    ConnectorSet rowConnectors=GetGISConnectors(element);
                    if(rowConnectors!=null)foreach(Connector connector in rowConnectors)
                    {
                        rowConnectorCount++;connectorTypes.Add(connector.ConnectorType.ToString());domains.Add(connector.Domain.ToString());
                        try{if(connector.IsConnected)rowConnectedCount++;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}
                        try{foreach(Connector reference in connector.AllRefs)if(reference.Owner!=null&&reference.Owner.Id!=element.Id)rowExternalRefs++;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}
                    }
                    FamilySymbol symbol=element.Symbol;
                    string[] values={element.Id.Value.ToString(),element.Category.Name,symbol.Family.Name,symbol.Name,mark,rowConnectorCount.ToString(System.Globalization.CultureInfo.InvariantCulture),rowConnectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),rowExternalRefs.ToString(System.Globalization.CultureInfo.InvariantCulture),string.Join(",",connectorTypes.Distinct()),string.Join(",",domains.Distinct()),GetGISParameter(element,"BIM_Sistema_Elemento"),GetGISParameter(element,"BIM_Bay"),location,GetGISParameter(symbol,"BIM_TensaoNominal_kV"),GetGISParameter(symbol,"BIM_TensaoClasse_kV"),GetGISParameter(symbol,"BIM_TensaoSecundaria_kV"),GetGISParameter(symbol,"BIM_TensaoClasseSecundaria_kV"),GetGISParameter(symbol,"BIM_CorrenteNominal_A"),GetGISParameter(symbol,"BIM_CorrenteReferenciaSecundaria_A"),GetGISParameter(symbol,"BIM_Potencia_MVA"),GetGISParameter(symbol,"BIM_Impedancia_pu"),GetGISParameter(symbol,"BIM_EstadoGeometria"),GetGISParameter(symbol,"BIM_Fabricante"),GetGISParameter(symbol,"BIM_ReferenciaCatalogo"),comments};
                    writer.WriteLine(string.Join(";",values.Select(GISCsv)));
                }
            }
            int electrical=elements.Count(x=>x.Category.Id==new ElementId(BuiltInCategory.OST_ElectricalEquipment));
            int generic=elements.Count(x=>x.Category.Id==new ElementId(BuiltInCategory.OST_GenericModel));
            int walls=new FilteredElementCollector(doc).OfClass(typeof(Wall)).GetElementCount();
            int floors=new FilteredElementCollector(doc).OfClass(typeof(Floor)).GetElementCount();
            List<DirectShape> directShapeElements=new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).Cast<DirectShape>().ToList();
            int directShapes=directShapeElements.Count;
            int modelCurves=new FilteredElementCollector(doc).OfClass(typeof(CurveElement)).Cast<CurveElement>().Count(x=>x is ModelCurve);
            int connectorCount=0,connectedConnectorCount=0,connectorsWithoutPhysicalStatus=0,electricalWithoutConnectors=0,missingIdentityMetadata=0;
            List<object> connectorDetails=new List<object>();
            foreach(FamilyInstance element in elements)
            {
                ConnectorSet connectors=GetGISConnectors(element);
                int count=connectors==null?0:connectors.Size;
                if(connectors!=null)foreach(Connector connector in connectors)
                {
                    bool connected=false;int externalRefs=0;List<string> refs=new List<string>();
                    try{connected=connector.IsConnected;if(connected)connectedConnectorCount++;}
                    catch(Autodesk.Revit.Exceptions.InvalidOperationException){connectorsWithoutPhysicalStatus++;}
                    try{foreach(Connector reference in connector.AllRefs)if(reference.Owner!=null&&reference.Owner.Id!=element.Id){externalRefs++;refs.Add(reference.Owner.Id.Value+":"+reference.ConnectorType+":"+reference.Domain);}}
                    catch(Autodesk.Revit.Exceptions.InvalidOperationException){}
                    connectorDetails.Add(new{element_id=element.Id.Value,mark=GISModelMark(element),connector_id=connector.Id.ToString(),domain=connector.Domain.ToString(),connector_type=connector.ConnectorType.ToString(),is_connected=connected,external_allrefs=externalRefs,allrefs=refs});
                }
                connectorCount+=count;
                if(element.Category.Id==new ElementId(BuiltInCategory.OST_ElectricalEquipment)&&count==0)electricalWithoutConnectors++;
                if(element.LookupParameter("BIM_Sistema_Elemento")==null||element.LookupParameter("BIM_Bay")==null||element.LookupParameter("BIM_Localizacao")==null)missingIdentityMetadata++;
            }
            int viewSheets=new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();
            int views3D=new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().Count(x=>!x.IsTemplate&&x.Name.StartsWith("3D-",StringComparison.OrdinalIgnoreCase));
            List<Autodesk.Revit.DB.Electrical.ElectricalSystem> systems=new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.ElectricalSystem)).Cast<Autodesk.Revit.DB.Electrical.ElectricalSystem>().ToList();
            int electricalSystems=systems.Count;
            int electricalSystemTerminalElements=systems.Sum(x=>x.Elements.Size);
            List<Autodesk.Revit.DB.Electrical.Wire> wires=new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.Wire)).Cast<Autodesk.Revit.DB.Electrical.Wire>().ToList();
            List<object> wireDetails=new List<object>();int wireConnectors=0,wireConnectedConnectors=0;
            foreach(Autodesk.Revit.DB.Electrical.Wire wire in wires)
            {
                ConnectorSet set=GetGISConnectors(wire);int count=0,connected=0;List<string> externalOwners=new List<string>();
                if(set!=null)foreach(Connector connector in set)
                {
                    count++;try{if(connector.IsConnected)connected++;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}
                    try{foreach(Connector reference in connector.AllRefs)if(reference.Owner!=null&&reference.Owner.Id!=wire.Id)externalOwners.Add(reference.Owner.Id.Value+":"+GISModelMark(reference.Owner));}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}
                }
                wireConnectors+=count;wireConnectedConnectors+=connected;
                Element ownerView=wire.OwnerViewId==ElementId.InvalidElementId?null:doc.GetElement(wire.OwnerViewId);
                wireDetails.Add(new{element_id=wire.Id.Value,mark=GISModelMark(wire),owner_view_id=wire.OwnerViewId.Value,owner_view=ownerView?.Name??"",wiring_type=wire.WiringType.ToString(),vertex_count=wire.NumberOfVertices,connector_count=count,connected_connector_count=connected,external_allrefs=externalOwners.Distinct().ToList(),comments=wire.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()??""});
            }
            bool auxWireConfirmed=wires.Any(w=>GISModelMark(w)=="AUX-UPS-01-WIRE"&&GetGISConnectors(w)!=null&&GetGISConnectors(w).Cast<Connector>().Count(c=>c.IsConnected)>=2);
            var systemDetails=systems.Select(s=>new{revit_element_id=s.Id.Value,system_id=s.Elements.Cast<Element>().Any(e=>GISModelMark(e)=="UPS-01")?"AUX-UPS-01":"UNASSIGNED",revit_class=s.GetType().Name,terminal_elements=s.Elements.Cast<Element>().Select(e=>new{element_id=e.Id.Value,mark=GISModelMark(e)}).ToList(),wire_2d_view_specific_confirmed=auxWireConfirmed,three_dimensional_cable_modeled=false,selected_panel="QAC-01",classification=auxWireConfirmed?"ELECTRICAL_SYSTEM_AND_VIEW_SPECIFIC_2D_WIRE":"LOGICAL_ELECTRICAL_SYSTEM_ONLY"}).ToList();
            List<Autodesk.Revit.DB.Electrical.CableTray> trays=new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.CableTray)).Cast<Autodesk.Revit.DB.Electrical.CableTray>().ToList();
            int trayConnectors=0,trayConnectedConnectors=0;List<object> trayDetails=new List<object>();
            foreach(Autodesk.Revit.DB.Electrical.CableTray tray in trays)
            {
                int count=0,connected=0;try{foreach(Connector connector in tray.ConnectorManager.Connectors){count++;try{if(connector.IsConnected)connected++;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}}}catch{}
                trayConnectors+=count;trayConnectedConnectors+=connected;
                trayDetails.Add(new{element_id=tray.Id.Value,mark=GISModelMark(tray),connector_count=count,connected_connector_count=connected,comments=tray.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()??""});
            }
            List<object> routeDetails=directShapeElements.Where(x=>{string m=GISModelMark(x);return m.IndexOf("ROUTE",StringComparison.OrdinalIgnoreCase)>=0||m.IndexOf("STUB",StringComparison.OrdinalIgnoreCase)>=0||m.IndexOf("-PH-",StringComparison.OrdinalIgnoreCase)>=0||m.IndexOf("BUSA-ENCLOSURE",StringComparison.OrdinalIgnoreCase)>=0||m.IndexOf("BUSB-ENCLOSURE",StringComparison.OrdinalIgnoreCase)>=0;})
                .Select(x=>new{element_id=x.Id.Value,mark=GISModelMark(x),comments=x.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()??""}).Cast<object>().ToList();
            var counts=elements.GroupBy(x=>x.Symbol.Family.Name+" | "+x.Symbol.Name).OrderBy(x=>x.Key).ToDictionary(x=>x.Key,x=>x.Count());
            List<string> warnings=new List<string>();try{warnings=doc.GetWarnings().Select(x=>x.GetDescriptionText()).ToList();}catch(Exception ex){warnings.Add("WARNING_EXTRACTION_FAILED: "+ex.Message);}
            string audit=JsonSerializer.Serialize(new{status="GENERATED_FROM_OPEN_REVIT_DOCUMENT",revit_document=doc.Title,elements=elements.Count,electrical_equipment=electrical,generic_model_families=generic,walls,floors,direct_shapes=directShapes,geometric_route_elements=routeDetails.Count,geometric_routes=routeDetails,model_curves=modelCurves,cable_trays=trays.Count,cable_tray_connectors=trayConnectors,cable_tray_connected_connectors=trayConnectedConnectors,cable_tray_elements=trayDetails,sheets=viewSheets,views_3d=views3D,mep_connectors=connectorCount,mep_connectors_isconnected_true=connectedConnectorCount,mep_connectors_without_physical_status=connectorsWithoutPhysicalStatus,mep_connector_details=connectorDetails,electrical_systems=electricalSystems,electrical_system_terminal_elements=electricalSystemTerminalElements,electrical_system_details=systemDetails,electrical_wire_2d_elements=wires.Count,electrical_wire_2d_connectors=wireConnectors,electrical_wire_2d_connected_connectors=wireConnectedConnectors,electrical_wire_2d_details=wireDetails,electrical_equipment_without_mep_connectors=electricalWithoutConnectors,instances_missing_required_identity_metadata=missingIdentityMetadata,document_warnings=warnings,family_type_counts=counts},new JsonSerializerOptions{WriteIndented=true});
            File.WriteAllText(Path.Combine(resultDir,"auditoria-modelo-r9.json"),audit,new UTF8Encoding(false));
            WriteGISConnectivityReport(elements,directShapeElements,trays,wires,resultDir);
            Log("GIS_MODEL_QUANTITIES_R9 family_instances="+elements.Count+" electrical_equipment="+electrical+" generic_families="+generic+" walls="+walls+" floors="+floors+" direct_shapes="+directShapes+" geometric_routes="+routeDetails.Count+" native_cable_trays="+trays.Count+" cable_tray_connectors="+trayConnectors+" cable_tray_connected="+trayConnectedConnectors+" electrical_connectors="+connectorCount+" electrical_isconnected_true="+connectedConnectorCount+" electrical_systems="+electricalSystems+" system_terminals="+electricalSystemTerminalElements+" 2d_wires="+wires.Count+" wire_connectors="+wireConnectors+" wire_connected="+wireConnectedConnectors+" sheets="+viewSheets+" views3d="+views3D+" warnings="+warnings.Count+" csv="+path);
        }

        private static ConnectorSet GetGISConnectors(Element element)
        {
            try{FamilyInstance instance=element as FamilyInstance;if(instance!=null&&instance.MEPModel!=null)return instance.MEPModel.ConnectorManager.Connectors;}catch{}
            try{MEPCurve curve=element as MEPCurve;if(curve!=null)return curve.ConnectorManager.Connectors;}catch{}
            return null;
        }

        private static string GISModelMark(Element element)
        {
            try{return element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()??"";}catch{return "";}
        }

        private static string GISGeometryAssociation(string mark,Element element)
        {
            if(element is Autodesk.Revit.DB.Electrical.Wire)return "NATIVE_ELECTRICAL_WIRE_2D_VIEW_SPECIFIC";
            if(element is Autodesk.Revit.DB.Electrical.CableTray)return "NATIVE_CABLETRAY_INFRASTRUCTURE_SEGMENT";
            if(element is DirectShape)return "GEOMETRIC_ENVELOPE_ONLY_NO_ELECTRICAL_CONNECTOR";
            if(mark=="QAC-01"||mark=="UPS-01")return "AUX-UPS-01_ELECTRICALSYSTEM; SEE_2D_WIRE_ROW; NO_3D_CABLE";
            if(mark.StartsWith("GIS-",StringComparison.OrdinalIgnoreCase)||mark.StartsWith("TR-",StringComparison.OrdinalIgnoreCase)||mark.StartsWith("QMT-",StringComparison.OrdinalIgnoreCase)||mark.StartsWith("BUS-MV",StringComparison.OrdinalIgnoreCase))return "EQUIPMENT_ENVELOPE_ONLY_NO_POWER_CONNECTOR";
            return "FAMILY_GEOMETRY_ONLY";
        }

        private static string GISDiagramAssociation(string mark)
        {
            if(mark.StartsWith("GIS-L01",StringComparison.OrdinalIgnoreCase))return "E-01: 138kV L01; selector A closed / B open";
            if(mark.StartsWith("GIS-L02",StringComparison.OrdinalIgnoreCase))return "E-01: 138kV L02; selector A open / B closed";
            if(mark.StartsWith("GIS-T01",StringComparison.OrdinalIgnoreCase))return "E-01: TR-01 bay; GIS double-bus selection";
            if(mark.StartsWith("GIS-T02",StringComparison.OrdinalIgnoreCase))return "E-01: TR-02 bay; GIS double-bus selection";
            if(mark.StartsWith("GIS-BC01",StringComparison.OrdinalIgnoreCase))return "E-01: 138kV bus coupler; breaker normally open assumption";
            if(mark=="TR-01"||mark.StartsWith("TR-01-",StringComparison.OrdinalIgnoreCase))return "E-01: TR-01 to QMT-A-IN, diagram relation";
            if(mark=="TR-02"||mark.StartsWith("TR-02-",StringComparison.OrdinalIgnoreCase))return "E-01: TR-02 to QMT-B-IN, diagram relation";
            if(mark.StartsWith("QMT-A",StringComparison.OrdinalIgnoreCase)||mark=="BUS-MV-A")return "E-01: 13.8kV section A and feeder IDs";
            if(mark.StartsWith("QMT-B",StringComparison.OrdinalIgnoreCase)||mark=="BUS-MV-B")return "E-01: 13.8kV section B and feeder IDs";
            if(mark=="QMT-TIE")return "E-01: 13.8kV tie normally open assumption";
            if(mark=="QAC-01"||mark=="UPS-01"||mark=="AUX-UPS-01-WIRE")return "E-06: AUX-UPS-01 system and 2D plan representation";
            if(mark.StartsWith("LEITO-",StringComparison.OrdinalIgnoreCase))return "E-05: cable tray route; no cable circuit";
            return "No power relation assigned; see equipment connectivity CSV";
        }

        private static void WriteGISConnectivityReport(List<FamilyInstance> instances,List<DirectShape> directShapes,List<Autodesk.Revit.DB.Electrical.CableTray> trays,List<Autodesk.Revit.DB.Electrical.Wire> wires,string resultDir)
        {
            bool auxWireConfirmed=wires.Any(w=>GISModelMark(w)=="AUX-UPS-01-WIRE"&&GetGISConnectors(w)!=null&&GetGISConnectors(w).Cast<Connector>().Count(c=>c.IsConnected)>=2);
            int trayConnectorCount=0,trayConnectedConnectorCount=0;
            foreach(Autodesk.Revit.DB.Electrical.CableTray tray in trays)try{foreach(Connector connector in tray.ConnectorManager.Connectors){trayConnectorCount++;try{if(connector.IsConnected)trayConnectedConnectorCount++;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}}}catch{}
            string path=Path.Combine(resultDir,"conectividade-equipamentos-r9.csv");
            using(StreamWriter writer=new StreamWriter(path,false,new UTF8Encoding(true)))
            {
                writer.WriteLine("element_id;categoria_revit;familia_ou_classe;marca;mep_connectors;isconnected_true;allrefs_external;model_geometry;diagram_relation;connection_not_implemented");
                foreach(Element element in instances.Cast<Element>().Concat(directShapes.Cast<Element>()).Concat(trays.Cast<Element>()).Concat(wires.Cast<Element>()).OrderBy(x=>x.Id.Value))
                {
                    ConnectorSet set=GetGISConnectors(element);int count=0,connected=0,refs=0;
                    if(set!=null)foreach(Connector connector in set)
                    {
                        count++;try{if(connector.IsConnected)connected++;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){}
                        try{foreach(Connector reference in connector.AllRefs)if(reference.Owner!=null&&reference.Owner.Id!=element.Id)refs++;}catch{}
                    }
                    string mark=GISModelMark(element),family=element is FamilyInstance fi?fi.Symbol.Family.Name:element.GetType().Name;
                    string pending=element is Autodesk.Revit.DB.Electrical.Wire?"No 3D cable; conductor size, upstream supply and protective coordination remain unmodeled":
                        mark=="QAC-01"||mark=="UPS-01"?"2D wire confirmed="+auxWireConfirmed+"; no 3D cable, upstream QAC supply, UPS output, DC circuit, battery or final loads":
                        element is Autodesk.Revit.DB.Electrical.CableTray?"Infrastructure only; open route ends and no conductors/equipment power connection":
                        mark.StartsWith("QMT-A-F",StringComparison.OrdinalIgnoreCase)||mark.StartsWith("QMT-B-F",StringComparison.OrdinalIgnoreCase)?"Outgoing cable, remote endpoint, conductor count and protection not modeled":
                        mark.StartsWith("GIS-",StringComparison.OrdinalIgnoreCase)||mark.StartsWith("TR-",StringComparison.OrdinalIgnoreCase)||mark.StartsWith("QMT-",StringComparison.OrdinalIgnoreCase)?"Power connectors and switching contacts are not electrically linked in Revit":
                        element is DirectShape?"Geometric envelope has no electrical connector or switching state":"No electrical relation declared for this equipment";
                    string[] values={element.Id.Value.ToString(),element.Category?.Name??"",family,mark,count.ToString(System.Globalization.CultureInfo.InvariantCulture),connected.ToString(System.Globalization.CultureInfo.InvariantCulture),refs.ToString(System.Globalization.CultureInfo.InvariantCulture),GISGeometryAssociation(mark,element),GISDiagramAssociation(mark),pending};
                    writer.WriteLine(string.Join(";",values.Select(GISCsv)));
                }
            }
            string rows="ID;De;Para;Revit electrical connection;3D geometry or infrastructure;Diagram/documentation;Pending\r\n"+
                "HV-L01;Rede 138 kV L01;GIS-L01-TERM;NONE;Terminal envelope only;E-01 selector 89A selected;External line route and physical primary conductor\r\n"+
                "HV-L02;Rede 138 kV L02;GIS-L02-TERM;NONE;Terminal envelope only;E-01 selector 89B selected;External line route and physical primary conductor\r\n"+
                "HV-BUSA;GIS selector 89A;GIS-BUS-A;NONE;Bus casing modules and geometric stubs;E-01;Internal bus conductor/contact and verified switching state\r\n"+
                "HV-BUSB;GIS selector 89B;GIS-BUS-B;NONE;Bus casing modules and geometric stubs;E-01;Internal bus conductor/contact and verified switching state\r\n"+
                "HV-BC;GIS-BUS-A;GIS-BUS-B;NONE;Bay component envelopes;E-01 52-BC with open normal-state assumption;Switching contacts and interlocks\r\n"+
                "HV-TR01;GIS-T01;TR-01-B-HV;NONE;GIS-TR01-HV-PH-A/B/C phase-separated DirectShape envelopes;E-01;Actual conductor/bus, terminal interfaces and manufacturer GA\r\n"+
                "HV-TR02;GIS-T02;TR-02-B-HV;NONE;GIS-TR02-HV-PH-A/B/C phase-separated DirectShape envelopes;E-01;Actual conductor/bus, terminal interfaces and manufacturer GA\r\n"+
                "MV-TR01;TR-01-B-MV;QMT-A-IN;NONE;TR01-MV-PH-A/B/C geometric route envelopes plus LEITO-MV-A native tray path;E-01 and section A;Cable/busduct design, ampacity, termination and tray connection\r\n"+
                "MV-TR02;TR-02-B-MV;QMT-B-IN;NONE;TR02-MV-PH-A/B/C geometric route envelopes plus LEITO-MV-B native tray path;E-01 and section B;Cable/busduct design, ampacity, termination and tray connection\r\n"+
                "MV-A;QMT-A-IN;BUS-MV-A;NONE;Cubicle and bar-module envelopes;E-01;Internal switchgear bus/contact and verified ratings\r\n"+
                "MV-B;QMT-B-IN;BUS-MV-B;NONE;Cubicle and bar-module envelopes;E-01;Internal switchgear bus/contact and verified ratings\r\n"+
                "MV-TIE;BUS-MV-A;BUS-MV-B;NONE;Tie cubicle envelope;E-01 52-TIE normally open assumption;Interlocks and physical switching state\r\n"+
                "MV-FDR-A;BUS-MV-A;FDR-A-F01...F05;NONE;Cable tray route only;E-01 feeder positions;Outgoing conductors, destinations, terminations and ampacity\r\n"+
                "MV-FDR-B;BUS-MV-B;FDR-B-F01...F05;NONE;Cable tray route only;E-01 feeder positions;Outgoing conductors, destinations, terminations and ampacity\r\n"+
                "AUX-UPS-01;QAC-01;UPS-01;"+(auxWireConfirmed?"ElectricalSystem + 2D Wire view-specific":"ElectricalSystem logical only")+";Wire is view-specific, not a 3D cable;E-06;3D cable, upstream source, conductor sizing, protection and complete UPS output network\r\n";
            File.WriteAllText(Path.Combine(resultDir,"relacoes-rede-r9.csv"),rows,new UTF8Encoding(true));
            string md="# Relatório de conectividade por equipamento — R9\n\n"+
                "A extração lê o RVT gerado no Revit. `IsConnected` e `AllRefs` são registrados separadamente; os elementos DirectShape, as famílias de equipamento e os sistemas elétricos não são tratados como equivalentes.\n\n"+
                "## Estado de execução\n\n"+
                "- A rede principal 138/13,8 kV não é uma rede elétrica conectada pela API MEP do Revit. Os equipamentos HV/MV não recebem conectores `PowerCircuit`.\n"+
                "- `AUX-UPS-01` tem "+(auxWireConfirmed?"um Wire nativo Revit com os dois conectores confirmados no plano E-06 e representação view-specific":"um ElectricalSystem lógico; não foi confirmada a conexão de fio no plano")+". Em ambos os casos não existe cabo 3D, dimensionamento do condutor ou prova de alimentação completa.\n"+
                "- As carcaças GIS, stubs, transições HV/MV e barras de invólucro são geometrias representativas sem contato interno ou porta elétrica.\n"+
                "- Foram extraídos "+trays.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)+" segmentos nativos CableTray, com "+trayConnectedConnectorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+" de "+trayConnectorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+" conectores marcados IsConnected. Essas juntas ligam infraestrutura a infraestrutura; não conectam os leitos aos equipamentos/circuitos.\n\n"+
                "## Relações da topologia\n\n"+
                "O CSV `relacoes-rede-r9.csv` lista as relações de cada etapa, separando conexão elétrica no RVT, geometria/infrastructure, relação mostrada em E-01 e pendências.\n\n"+
                "O CSV `conectividade-equipamentos-r9.csv` tem uma linha para cada FamilyInstance, DirectShape de rota, trecho CableTray e Wire, com ElementId, marca, contagem de conectores, `IsConnected`, referências externas e relação declarada.\n\n"+
                "## Pendências principais\n\n"+
                "1. Condutores primários, contatos internos GIS, ligação física aos terminais dos transformadores, condutores/barramento MT e saídas dos dez alimentadores não estão implementados como circuito ou cabo no RVT.\n"+
                "2. As juntas internas de leito só são consideradas conectadas quando `ConnectTo`/`IsConnected` foram confirmados; terminais externos continuam abertos, e não há ocupação de cabos nem conexão dos leitos aos equipamentos.\n"+
                "3. A relação lógica auxiliar não inclui circuitos QCC/bateria, saída CC da UPS ou cargas finais.\n";
            File.WriteAllText(Path.Combine(resultDir,"conectividade-r9.md"),md,new UTF8Encoding(false));
        }

        private static string GetGISParameter(Element element,string name)
        {
            Parameter parameter=element.LookupParameter(name);if(parameter==null)return "";
            switch(parameter.StorageType)
            {
                case StorageType.String:return parameter.AsString()??"";
                case StorageType.Double:return parameter.AsDouble().ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
                case StorageType.Integer:return parameter.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:return "";
            }
        }

        private static string GISCsv(string value)=>"\""+(value??"").Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";

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
                    if(Number==19)return "Subestacao GIS com dupla barra, bays, transformadores, seções MT, infraestrutura nativa CableTray e ElectricalSystem/Wire 2D auxiliar; caminhos HV/MT em geometria sem conectores de potência.";
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
