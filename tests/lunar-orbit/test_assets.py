import pathlib,re,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
class AssetIntegrityTests(unittest.TestCase):
    def test_asset_metadata_and_saved_scene_references(self):
        assets=ROOT/'Assets/GraceLunarOrbit';ids={}
        for meta in assets.rglob('*.meta'):
            guid=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(),re.M)
            self.assertIsNotNone(guid,str(meta))
            guid=guid[1];self.assertNotIn(guid,ids,'duplicate GUID');ids[guid]=meta
            self.assertTrue(pathlib.Path(str(meta)[:-5]).exists(),'orphan meta')
        for file in assets.rglob('*'):
            if file.is_file() and file.suffix!='.meta':
                self.assertTrue(pathlib.Path(str(file)+'.meta').exists(),'missing meta: '+str(file))
        scene=assets/'Scenes/LunarOrbitOverview.unity'
        for guid in re.findall(r'guid: ([a-f0-9]{32})',scene.read_text()):
            self.assertIn(guid,ids,'unresolved scene GUID')
        refs=re.findall(r'--- !u!\d+ &(\d+)',scene.read_text())
        self.assertEqual(len(refs),len(set(refs)),'duplicate file IDs')
        for fid in re.findall(r'\{fileID: (\d+)\}',scene.read_text()):
            self.assertTrue(fid=='0' or fid in refs,'unresolved local file ID')
if __name__=='__main__':unittest.main()
