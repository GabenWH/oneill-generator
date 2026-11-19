"""Source wiring checks; Unity's Tools menu runs the real collider smoke test."""
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[2] / 'Assets/Scripts'
def source(name):
    f = ROOT / (name + '.cs')
    return f.read_text() if f.exists() else ''
class GroundEffect(unittest.TestCase):
    def test_flight_uses_fixed_step_and_radial_query(self):
        drone = source('DroneController')
        self.assertIn('ApplyGroundEffect();', drone)
        self.assertIn('GroundEffectSurface.TryMeasure', drone)
        self.assertIn('Vector3.ProjectOnPlane', source('GroundEffectSurface'))
    def test_land_window_and_water_are_eligible(self):
        query = source('GroundEffectSurface')
        self.assertIn('GetComponentInParent<CylinderChunk>()', query)
        self.assertIn('GetComponentInParent<CylinderWindowChunk>()', query)
        self.assertIn('class CylinderWaterChunk : CylinderChunk', source('CylinderWaterChunk'))
    def test_water_boundary_does_not_discard_glass_or_dry_land(self):
        query = source('GroundEffectSurface')
        self.assertIn('hit.distance >= distance', query)
        self.assertNotIn('WaterRadius', query)
        self.assertIn('QueryTriggerInteraction.Ignore', query)
    def test_water_fixture_is_opt_in_and_uses_shared_chunk_generation(self):
        self.assertIn('if (includeGroundEffectTestWater)', source('CylinderChunkManager'))
        self.assertIn('water.Generate(', source('CylinderChunkManager'))
        self.assertIn('manager.includeGroundEffectTestWater = includeGroundEffectTestWater;', source('WorldBootstrap'))
if __name__ == '__main__':
    unittest.main()
