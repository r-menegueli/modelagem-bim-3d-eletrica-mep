import unittest
from pathlib import Path


SOURCE = Path(__file__).resolve().parents[1] / "scripts" / "revit-addin" / "RevitPortfolioGenerator.cs"


class RevitGeneratorContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = SOURCE.read_text(encoding="utf-8")

    def test_protection_panel_declares_separate_dc_control_and_ac_heater_interfaces(self):
        self.assertIn("ElectricalSystemType.Controls,leftFace.Reference", self.source)
        self.assertIn('SetGISConnectorStudyData(controlInput,.125', self.source)
        self.assertIn("ElectricalSystemType.PowerCircuit,rightFace.Reference", self.source)
        self.assertIn('GIS_AUX_AC_HEATER_220V', self.source)
        self.assertIn('AuxiliaryAcVoltageKv=.220', self.source)
        self.assertIn('if(familyKey=="ProtectionPanel")return .125;', self.source)

    def test_auxiliary_circuits_select_only_the_named_ac_heater_connector(self):
        self.assertIn('c.ElectricalSystemType==Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerCircuit&&(c.Description??"").StartsWith("GIS_AUX_AC_HEATER_220V"', self.source)
        self.assertIn('QAC-01 → aquecedores auxiliares associados aos painéis', self.source)
        self.assertIn('interface `Controls` de 125 Vcc, ainda sem circuito CC', self.source)

    def test_equipment_connectivity_classifies_panels_as_auxiliary_not_transformer_power(self):
        self.assertIn('mark=="TR-01-CTRL"||mark=="TR-02-CTRL"', self.source)
        self.assertIn('mark.StartsWith("P-",StringComparison.OrdinalIgnoreCase)', self.source)
        self.assertIn('E-06: protection/control panel; Controls 125Vdc; separate study heater input 220Vac; see circuit audit', self.source)
        self.assertIn('220 Vca cabinet-heater PowerCircuit logically confirmed; assumed study load only; no 3D cable', self.source)
        self.assertIn('220 Vca cabinet-heater PowerCircuit not confirmed; no 3D cable', self.source)

    def test_auxiliary_failed_attempt_discards_transient_revit_measurements(self):
        self.assertIn('audit.revit_voltage_V=null;audit.revit_true_load_W=null;audit.revit_apparent_power_VA=null;', self.source)
        self.assertIn('audit.revit_apparent_current_A=null;audit.revit_power_factor=null;', self.source)
        self.assertIn('Revit calculation: NOT CONFIRMED; transient values from the rolled-back attempt are discarded', self.source)

    def test_m01_displays_reproduced_r8_auxiliary_and_battery_estimates_with_limits(self):
        self.assertIn('servicos-auxiliares-resultados-r8.json', self.source)
        self.assertIn('GisAuxiliaryCalc("AUX-AC-11","0.000")', self.source)
        self.assertIn('GisAuxiliaryCalc("DC-03","0.00")', self.source)
        self.assertIn('GisAuxiliaryCalc("DC-04","0")', self.source)
        self.assertIn('GisAuxiliaryCalc("UPS-03","0.000")', self.source)
        self.assertIn('Linha de base R8 com cargas, FP, missão e fatores assumidos;', self.source)
        self.assertIn('Não é dimensionamento aprovado.', self.source)

    def test_heater_load_provenance_does_not_attribute_assumptions_to_ied_loads(self):
        self.assertIn('alocacao como aquecedores de painel e hipotese, nao carga de IED', self.source)
        self.assertIn('Quantidade e placa dos aquecedores permanecem pendentes.', self.source)
        self.assertNotIn('R8 AUX-AC-06: 11 x 0.15 kW; FP 0.90 assumido; listas de cargas dos IEDs pendentes', self.source)

    def test_auxiliary_ac_load_connector_is_promoted_before_load_calculation(self):
        family_branch = self.source[
            self.source.index('else if(spec.Key=="ProtectionPanel")'):
            self.source.index('ReferenceArray widthRefs', self.source.index('else if(spec.Key=="ProtectionPanel")'))
        ]
        promote = family_branch.index('auxiliaryInput.AssignAsPrimary();')
        confirm = family_branch.index('if(!auxiliaryInput.IsPrimary)')
        calculate = family_branch.index('SetGISConnectorStudyLoadData(auxiliaryInput,150.0,.90)')
        self.assertLess(promote, confirm)
        self.assertLess(confirm, calculate)
        load_helper = self.source[
            self.source.index('private static bool SetGISConnectorStudyLoadData('):
            self.source.index('private static bool TrySetGISConnectorPhase1Current(')
        ]
        self.assertIn('if(connector==null||!connector.IsPrimary)', load_helper)
        self.assertIn('PRIMARY_CONNECTOR_NOT_CONFIRMED', load_helper)

    def test_equipment_connectivity_csv_exports_connector_domains(self):
        self.assertIn('marca;mep_connectors;dominios;isconnected_true;allrefs_external', self.source)
        self.assertIn('connectorDomains.Add(connector.Domain.ToString())', self.source)

    def test_connector_audit_reads_system_type_only_for_electrical_domain(self):
        self.assertIn('system_type=GISConnectorSystemType(connector)', self.source)
        self.assertIn('if(connector.Domain!=Domain.DomainElectrical)return "NOT_APPLICABLE";', self.source)
        self.assertIn('return "UNAVAILABLE:"+ex.GetType().Name;', self.source)

    def test_wire_confirmation_and_system_measurements_tolerate_unavailable_api_values(self):
        self.assertIn('GISConnectedWireConnectorCount(w)>=2', self.source)
        self.assertIn('catch(Autodesk.Revit.Exceptions.InvalidOperationException){}', self.source)
        self.assertIn('model_voltage_V=GISSystemAuditValue(s,"Voltage",()=>UnitUtils.ConvertFromInternalUnits(s.Voltage,UnitTypeId.Volts))', self.source)
        self.assertIn('model_power_factor=GISSystemAuditValue(s,"PowerFactor",()=>s.PowerFactor)', self.source)
        self.assertIn('GIS_SYSTEM_VALUE_READ_FAILED system_id=', self.source)

    def test_unifilar_shows_earthing_switches_and_distinguishes_metering_positions(self):
        self.assertIn('DrawGISEarthSwitchBranch(doc,view,cx,4.16,tags[i]+"-89G O*"', self.source)
        self.assertIn('labels[i].EndsWith("-MED"', self.source)
        self.assertIn('TC*: placeholders; CT-BC01* em série no acoplador.', self.source)

    def test_coupler_ct_is_in_series_and_kept_provisional_in_e01(self):
        single_line = self.source[
            self.source.index('private static void DrawGISSingleLine('):
            self.source.index('private static void DrawGISHorizontalDisconnector(', self.source.index('private static void DrawGISSingleLine('))
        ]
        coupler = single_line[single_line.index('if(i==2)'):single_line.index('continue;', single_line.index('if(i==2)'))]
        self.assertIn('const double busA=7.75,busB=5.75;', single_line)
        self.assertIn('DrawLine(doc,view,cx,6.94,cx,6.74,line,0);DrawGISCTSymbol(doc,view,cx,6.62,text,line,"CT-BC01*")', coupler)
        self.assertIn('DrawLine(doc,view,cx,6.50,cx,6.31,line,0);DrawGISDisconnectorSymbol(doc,view,cx,6.20,"89B C*",text,line)', coupler)
        self.assertIn('DrawLine(doc,view,cx,6.09,cx,busB,line,0)', coupler)
        self.assertNotIn('CT-BC01* placeholder', coupler)
        self.assertIn('CT-BC01* desenhado em série; relação, núcleos, fases e posição pendentes; 87B/50BF em estudo.', self.source)

    def test_unifilar_marks_normal_open_ties_and_assumed_bus_selector_positions(self):
        self.assertIn('52-BC e 52-TIE abertos; 89T-A/B fechadas; 89G abertas', self.source)
        self.assertIn('selectB?"89A O*":"89A C*"', self.source)
        self.assertIn('selectB?"89B C*":"89B O*"', self.source)
        self.assertIn('"52-BC O*"', self.source)
        self.assertIn('"52-TIE O*"', self.source)
        self.assertIn('"89T-A C*"', self.source)
        self.assertIn('"89T-B C*"', self.source)

    def test_primary_network_limits_remain_explicit(self):
        self.assertIn('A rede principal 138/13,8 kV não é uma rede elétrica conectada pela API MEP do Revit.', self.source)
        self.assertIn('circuitos lógicos QAC-01', self.source)

    def test_gis_bay_frame_uses_published_f35_envelope_and_keeps_order_ga_pending(self):
        self.assertIn('TypeName="GIS_BAY_F35_145KV_DIGITAL_SF6_BAY_ENVELOPE",Width=0.8,Depth=3.7,Height=2.5', self.source)
        self.assertIn('GeometryStatus="PUBLISHED_BAY_ENVELOPE_REFERENCE"', self.source)
        self.assertIn('published envelope 800 x 3,700 x 2,500 mm', self.source)
        self.assertIn('not a project-specific order GA', self.source)
        self.assertIn('GISFamilyDirectoryName = "reconstrucao-r23-f35-envelope-review"', self.source)
        self.assertIn('r20-23-candidate', self.source)
        self.assertIn('Path.Combine(outDir,"familias",GISFamilyDirectoryName,familyStem+".rfa")', self.source)
        self.assertIn('closed envelope uses qualitative external access/control/drive-face details', self.source.lower())
        self.assertNotIn('Depth=5.7,Height=3.6', self.source)

    def test_q01_uses_short_f35_geometry_label_that_fits_status_column(self):
        self.assertIn('case "PUBLISHED_BAY_ENVELOPE_REFERENCE": return "F35 REF.";', self.source)

    def test_published_f35_envelope_is_consistent_in_sheet_instance_and_readme_notes(self):
        self.assertIn('F35-145: envelope de baia publicado 800 x 3.700 x 2.500 mm.', self.source)
        self.assertIn('envelope F35-145 publicado: 0,8 x 3,7 x 2,5 m (L x P x A)', self.source)
        self.assertIn('a brochura p. 6 publica envelope de baia 800 x 3.700 x 2.500 mm', self.source)
        self.assertNotIn('profundidade e altura estimadas, nao dimensoes do catalogo', self.source.lower())
        self.assertNotIn('profundidade/altura estimadas', self.source.lower())
        self.assertNotIn('somente a largura de baia de 800 mm é publicada', self.source)

    def test_primary_route_geometry_is_grouped_by_phase_and_audits_equipment_envelopes(self):
        self.assertIn('double tr01GISExitX=18.8+(phase-1)*.20;', self.source)
        self.assertIn('double tr02GISExitX=20.4+(phase-1)*.20;', self.source)
        self.assertIn('double tr02ApproachX=60.72+phase*.24;', self.source)
        self.assertIn('double tr02CabinetX=59.8-.24+phase*.24;', self.source)
        self.assertIn('AddGISPhaseRoutePolyline(doc,"GIS-TR01-HV-PH-"+phaseName+"-ROUTE"', self.source)
        self.assertIn('M(tr01CabinetX),M(14.77),M(.55)', self.source)
        self.assertIn('GIS_PHASE_ROUTE_JSON=', self.source)
        self.assertIn('phase_label_basis="GEOMETRIC_LANE_LABEL_ONLY_PHASE_ASSIGNMENT_UNVERIFIED"', self.source)
        self.assertIn('GISPointToEquipmentBoundingBoxGapMm', self.source)
        self.assertIn('terminal_port_identified=false,native_electrical_system_confirmed=false', self.source)
        self.assertIn('Open(doc,north,18.50,y2,19.63,y2,2.0,3.1,', self.source)
        self.assertIn('Open(doc,north,42.80,y2,43.92,y2,.15,1.05,', self.source)
        self.assertIn('route_wall_opening_details=routeWallOpeningDetails', self.source)
        self.assertIn('EXPECTED_4_ROUTE_WALL_OPENINGS_FOUND_', self.source)
        self.assertIn('ClassifyGISRouteOpeningByBounds(bounds)', self.source)
        self.assertIn('classification_source=String.IsNullOrWhiteSpace(routeReference)?"UNCLASSIFIED_BOUNDS":"API_BOUNDING_BOX_MATCH"', self.source)
        self.assertIn('unclassified_wall_opening_details=unclassifiedWallOpeningDetails', self.source)
        self.assertNotIn('EXPECTED_FOUR_OPENING_COUNT', self.source)
        self.assertNotIn('routeOpeningCountFallback', self.source)
        self.assertNotIn('new XYZ(M(tr01ApproachX),M(31.5),M(.55))', self.source)
        self.assertNotIn('new XYZ(M(tr01CabinetX),M(15.0),M(.55))', self.source)

    def test_r20_23_candidate_keeps_outputs_isolated_and_focus_views_hide_route_geometry(self):
        self.assertIn('"r20-23-candidate"', self.source)
        self.assertIn('17.5,16.7,22.2,21.0,24', self.source)
        helper_start = self.source.index('private static void HideGISRouteGeometry(')
        helper_end = self.source.index('private static void CreateGISSheets(', helper_start)
        helper = self.source[helper_start:helper_end]
        self.assertIn('x.ApplicationId,"PortfolioBIM.GIS.R20.GeometryOnly"', helper)
        self.assertIn('view.HideElements(routeIds)', helper)
        self.assertIn('private static void ApplyGISBayEnvelopeReviewStyle(', helper)
        self.assertIn('envelopes.Count!=5', helper)
        self.assertIn('ghost.SetSurfaceTransparency(82)', helper)
        sheets = self.source[
            self.source.index('private static void CreateGISSheets('):
            self.source.index('private static void AddGISPlanNote(', self.source.index('private static void CreateGISSheets('))
        ]
        self.assertIn('View3D transformerSheet3D=CreateGIS3DView(doc,"E-03 - Vista 3D TR-01 e terminais R20",27.4,31.0,-.1,36.8,38.6,6.1)', sheets)
        self.assertIn('View3D mvSheet3D=CreateGIS3DView(doc,"E-04 - Vista 3D cubiculos MT secao A R20",43.2,13.0,-.1,49.8,15.2,3.3)', sheets)
        self.assertIn('ApplyGISBayEnvelopeReviewStyle(doc,gisBay)', sheets)
        self.assertIn('HideGISRouteGeometry(doc,transformerSheet3D)', sheets)
        self.assertIn('HideGISRouteGeometry(doc,mvSheet3D)', sheets)
        self.assertEqual(sheets.count('HideGISRouteGeometry(doc,'), 2)
        self.assertIn('CreateSheet(doc,titleBlock,"E-03","Transformadores e terminais",transformerSheet3D', sheets)
        self.assertIn('CreateSheet(doc,titleBlock,"E-04","Cubículos MT, seção A",mvSheet3D', sheets)

    def test_3d_view_audit_records_visibility_and_graphics_state(self):
        self.assertIn('temporaryHideIsolateActive=view.IsTemporaryHideIsolateActive()', self.source)
        self.assertIn('hiddenElementCount=elements.Count(x=>{try{return x.IsHidden(view);}', self.source)
        self.assertIn('new FilteredElementCollector(doc,view.Id).WhereElementIsNotElementType().ToElements()', self.source)
        self.assertIn('view_template_name=templateElement?.Name??""', self.source)

    def test_3d_view_orientation_projects_up_vector_perpendicular_to_forward(self):
        self.assertIn(
            'XYZ upDirection=XYZ.BasisZ.Subtract(forward.Multiply(XYZ.BasisZ.DotProduct(forward))).Normalize();',
            self.source,
        )
        self.assertIn('new ViewOrientation3D(eye,upDirection,forward)', self.source)

    def test_r20_outputs_are_not_overwritten_and_project_docs_follow_successful_pdf_export(self):
        method = self.source[
            self.source.index('private static void BuildGISProject('):
            self.source.index('private static ElementId CreateGISFloor(', self.source.index('private static void BuildGISProject('))
        ]
        self.assertIn('if(protectedOutputs.Any(File.Exists)', method)
        self.assertIn('new SaveAsOptions{OverwriteExistingFile=false}', method)
        export_ok = method.index('if(!ok)throw new InvalidOperationException("Revit returned false while exporting GIS R20 PDF.");')
        write_docs = method.index('WriteGISProjectFiles(spec,outDir,baseName);')
        self.assertLess(export_ok, write_docs)

    def test_r20_sheet_layout_uses_compact_schedule_text_and_full_width_3d_views(self):
        self.assertIn('CreateSheet(doc,titleBlock,"Q-01","Equipamentos e conectividade",quantities,text,"",spec,baseName)', self.source)
        self.assertIn('if(!String.IsNullOrWhiteSpace(notes))TextNote.Create(doc,s.Id,new XYZ(M(.255),M(.246),0),M(.143),notes,text);', self.source)
        self.assertIn('ElementId compactEngineeringText=GetFooterText(doc,text);', self.source)
        self.assertIn('view.Scale=view.Name.StartsWith("E-01",StringComparison.OrdinalIgnoreCase)?55:', self.source)
        self.assertIn('view.Name.StartsWith("Q-01",StringComparison.OrdinalIgnoreCase)?60:', self.source)
        self.assertIn('view.Name.StartsWith("M-01",StringComparison.OrdinalIgnoreCase)?70:75;', self.source)
        self.assertIn('DrawGISSingleLine(doc,single,compactEngineeringText,note,power)', self.source)
        self.assertIn('DrawGISReconstructionCalculationTable(doc,calculations,compactEngineeringText,note,power,data)', self.source)
        self.assertIn('DrawGISQuantitySummary(doc,quantities,compactEngineeringText)', self.source)
        self.assertIn('double maxWidth=.386;', self.source)
        self.assertIn('Vista 3D ampliada do bay GIS-T01.', self.source)
        self.assertNotIn('CreateGISSupplementalViewport(doc,bim02', self.source)
        self.assertNotIn('CreateGISSupplementalViewport(doc,bim03', self.source)
        self.assertNotIn('CreateGISSupplementalViewport(doc,bim04', self.source)
        self.assertIn('DrawGISBaySheetDetail(doc,e02,text,note,power)', self.source)
        self.assertIn('DrawGISTransformerSheetDetail(doc,e03,text,note,power)', self.source)
        self.assertIn('DrawGISMVSheetDetail(doc,e04,text,note,power)', self.source)
        self.assertNotIn('.515,.128,.185,.105', self.source)

    def test_gis_bay_frame_is_closed_external_shell_without_internal_oem_geometry(self):
        start = self.source.index('private static void AddGISFamilyDetails(')
        branch_start = self.source.index('else if(spec.Key=="GIS_BayFrame")', start)
        branch_end = self.source.index('else if(spec.Key=="SteelSupport")', branch_start)
        branch = self.source[branch_start:branch_end]
        self.assertIn('AddGISFamilyBox(fdoc,x0,y0,skin,d,baseTop,wallHeight)', branch)
        self.assertIn('AddGISFamilyBox(fdoc,x0+w-skin,y0,skin,d,baseTop,wallHeight)', branch)
        self.assertIn('double controlHeight=h*.48;', branch)
        self.assertIn('access, local control and spring drive', branch)
        self.assertNotIn('AddGISFamilyTube', branch)
        self.assertNotIn('AddGISFamilyAnnulus', branch)
        self.assertIn('no internal oem components', self.source.lower())
        self.assertIn('closed envelope uses qualitative external access/control/drive-face details', self.source.lower())

    def test_family_inventory_exports_type_dimensions_in_meters(self):
        self.assertIn('largura_tipo_m;profundidade_tipo_m;altura_tipo_m', self.source)
        self.assertIn('GetGISLengthParameterMeters(symbol,"BIM_Largura")', self.source)
        self.assertIn('GetGISLengthParameterMeters(symbol,"BIM_Profundidade")', self.source)
        self.assertIn('GetGISLengthParameterMeters(symbol,"BIM_Altura")', self.source)

    def test_geometry_audit_keeps_phase_route_envelopes_separate_from_conductor_entities(self):
        self.assertIn('physical_conductor_details=physicalConductorCandidates', self.source)
        self.assertIn('geometric_phase_route_details=phaseRouteDetails', self.source)
        self.assertIn('representation_role="GEOMETRIC_PHASE_ROUTE_ONLY"', self.source)
        self.assertIn('GISSolidCountInternal(geometry)', self.source)
        self.assertIn('endpoint_gap_basis="Closest distance from route centerline endpoint to get_BoundingBox(null)', self.source)
        self.assertIn('if(!mark.StartsWith("COND-",StringComparison.OrdinalIgnoreCase))continue;', self.source)
        self.assertIn('GIS-T01-CT-TO-TR-01-HV', self.source)
        self.assertIn('TR-01-MV-ROUTE', self.source)


if __name__ == "__main__":
    unittest.main()
