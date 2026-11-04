import unittest
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'Assets/VWater'
class CoreContract(unittest.TestCase):
 def test_public_skim_api(self):
  c=(r/'Scripts/VWaterDynamicsManager.cs').read_text(encoding='utf-8-sig')
  self.assertIn('public bool AddSkim(',c)
 def test_race_free_clamped_read(self):
  c=(r/'Shaders/VWaterCompute.compute').read_text(encoding='utf-8-sig')
  self.assertIn('int2 ClampIndex(',c)
  self.assertNotIn('float2 blurred = WaterBuffer[',c)
 def test_clear_and_release_buffers(self):
  c=(r/'Scripts/VWaterDynamicsManager.cs').read_text(encoding='utf-8-sig')
  self.assertIn('ClearBuffer(waterBuffer)',c)
  self.assertIn('ReleaseBuffers()',c)
 def test_directional_foam(self):
  c=(r/'Shaders/VWaterCompute.compute').read_text(encoding='utf-8-sig')
  self.assertIn('skimVelocity',c)
  self.assertIn('skimVelocityFoam',c)
 def test_flat_water_does_not_erase_skim_foam(self):
  c=(r/'Shaders/VWaterCompute.compute').read_text(encoding='utf-8-sig')
  self.assertIn('max(0.0, clamp(wb.r, 0.0, 10.0) - 0.02)',c)
 def test_explicit_stable_slots_and_culling_clear(self):
  c=(r/'Scripts/VWaterDynamicsManager.cs').read_text(encoding='utf-8-sig')
  self.assertIn('float foam, int slot)',c)
  self.assertIn('skimIncoming[slot]',c)
  self.assertIn('if (!nearAnyWater) ClearSkimInputs();',c)
  self.assertIn('private void OnDisable()',c)
 def test_missing_camera_rejects_and_clears(self):
  c=(r/'Scripts/VWaterDynamicsManager.cs').read_text(encoding='utf-8-sig')
  self.assertIn('!nearAnyWater || MainCamera == null || slot < 0',c)
  self.assertIn('if (MainCamera == null) { ClearSkimInputs(); return; }',c)
 def test_snapshot_stays_pending_until_dispatch(self):
  c=(r/'Scripts/VWaterDynamicsManager.cs').read_text(encoding='utf-8-sig')
  self.assertIn('skimPendingCount',c)
  batch=c.index('for (int i = 0; i < steps; i++) UpdateSimulation(deltaTime);')
  self.assertGreater(c.index('ClearSkimSnapshot();',batch),batch)
if __name__ == '__main__':unittest.main(verbosity=2)
