import json,subprocess,sys,tempfile,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
class Layer4ConverterTests(unittest.TestCase):
 def test_compiles_frozen_layer4_contract(self):
  with tempfile.TemporaryDirectory() as d:
   out=Path(d)/'draft.json';subprocess.run([sys.executable,'-m','Tools.migration.layer4_map_nodes_converter','--export',str(ROOT/'Tools/migration/out/pure-run-layer4-map-nodes-v1.unity.json'),'--specification',str(ROOT/'Tools/migration/manifest/export-batches/pure-run-layer4-map-nodes-v1.json'),'--output',str(out)],cwd=ROOT,check=True)
   value=json.loads(out.read_text(encoding='utf-8'));self.assertEqual('encounter.pure-run.n4',value['encounter']['contentId']);self.assertEqual(['unit.pure-run.goat-ranged','unit.pure-run.goat-ranged','unit.pure-run.goat-aoe','unit.pure-run.goat-charger'],value['encounter']['monsters']);self.assertEqual(4,len(value['map']['layer4']));self.assertEqual(3,len(value['events']));self.assertIn('Special',value['excluded'])
