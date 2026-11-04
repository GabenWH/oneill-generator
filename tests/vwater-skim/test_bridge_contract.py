"""Static wiring checks only; not a substitute for Unity compilation or runtime."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[2]
B=ROOT/'Assets/GraceVWaterOriginalTest/Runtime/GraceOriginalVWaterFanBridge.cs'
E=ROOT/'Assets/GraceVWaterOriginalTest/Editor/GraceOriginalVWaterSceneBuilder.cs'
class BridgeContract(unittest.TestCase):
 def test_bridge_submits_moving_skim_to_adapted_core(self):
  s=B.read_text(encoding='utf-8-sig')
  self.assertIn('originalDynamics.AddSkim(',s)
  self.assertIn('hitVelocity',s)
  self.assertIn('skimFoam',s)
  self.assertIn('CallsAcceptedThisFrame',s)
 def test_builder_presents_adapted_scene_without_original_hash_gate(self):
  s=E.read_text(encoding='utf-8-sig')
  self.assertIn('Create Skimming Drone Test',s)
  self.assertNotIn('try{ValidateOriginalSources();}',s)
if __name__=='__main__': unittest.main()
