import pathlib, subprocess, tempfile, unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
class PatchTests(unittest.TestCase):
    def test_literal_filter_patch_preserves_rest_of_function(self):
        before=(ROOT/'docs/lunar-orbit/Reference/ApplyGroundEffect.original.txt').read_text()
        expected=before.replace('GetComponent<LandChunk>() == null','GetComponent<CylinderChunk>() == null')
        actual=self.apply(before,'01-cylinder-chunk-filter.patch')
        self.assertEqual(actual,expected)
    def test_optional_water_guard_applies_after_literal_patch(self):
        before=(ROOT/'docs/lunar-orbit/Reference/ApplyGroundEffect.original.txt').read_text()
        first=self.apply(before,'01-cylinder-chunk-filter.patch')
        actual=self.apply(first,'02-OPTIONAL-land-only-radius-exclusion.patch')
        self.assertEqual(actual,first.replace('if (surfaceRadius.magnitude >= world.WaterRadius) continue;',
            'if (hit.collider.GetComponent<LandChunk>() != null &&\n                surfaceRadius.magnitude >= world.WaterRadius) continue;'))
    def test_wrong_source_is_rejected_without_changes(self):
        before='class Unrelated {}\n'
        with tempfile.TemporaryDirectory() as tmp:
            p=pathlib.Path(tmp)/'Assets/Scripts/DroneController.cs';p.parent.mkdir(parents=True);p.write_text(before)
            proc=subprocess.run(['git','apply','--check',str(ROOT/'docs/lunar-orbit/Patches/01-cylinder-chunk-filter.patch')],cwd=tmp,capture_output=True)
            self.assertNotEqual(proc.returncode,0)
            self.assertEqual(p.read_text(),before)
    def apply(self,before,patch):
        patch=ROOT/'docs/lunar-orbit/Patches'/patch
        self.assertTrue(patch.exists(),'patch not implemented')
        with tempfile.TemporaryDirectory() as tmp:
            p=pathlib.Path(tmp)/'Assets/Scripts/DroneController.cs';p.parent.mkdir(parents=True);p.write_text(before)
            subprocess.run(['git','apply','--check',str(patch)],cwd=tmp,check=True)
            subprocess.run(['git','apply',str(patch)],cwd=tmp,check=True)
            return p.read_text()
if __name__=='__main__':unittest.main()
