using System.Collections.Generic;
using UnityEngine;

/// <summary>Samples a moving fan by distance and time, including between frames.</summary>
public sealed class CylinderWakeTrail
{
    private bool active;
    private Vector2 previous;
    private float previousTime, distanceSinceEmission, timeSinceEmission;

    // Samples are (surface arc, cylinder Z, emission time), in habitat coordinates.
    public void Sample(Vector2 point, float now, float spacing, List<Vector3> samples, float interval = .5f)
    {
        samples.Clear();
        spacing = Mathf.Max(.2f, spacing);
        float delta = now - previousTime;
        float distance = Vector2.Distance(previous, point);
        if (!active || delta > .25f || distance > 32f)
        {
            distanceSinceEmission = timeSinceEmission = 0f;
            samples.Add(new Vector3(point.x, point.y, now));
        }
        else if (delta > 0f)
        {
            float fraction = 0f;
            for (int sample = 0; sample < 32; sample++)
            {
                float byDistance = distance > .00001f ? Mathf.Max(0f, spacing - distanceSinceEmission) / distance : float.PositiveInfinity;
                float byTime = Mathf.Max(0f, Mathf.Max(.08f, interval) - timeSinceEmission) / delta;
                float advance = Mathf.Min(byDistance, byTime);
                float remaining = 1f - fraction;
                if (advance > remaining + .000001f)
                {
                    distanceSinceEmission += distance * remaining;
                    timeSinceEmission += delta * remaining;
                    break;
                }
                fraction = Mathf.Min(1f, fraction + advance);
                Vector2 location = Vector2.Lerp(previous, point, fraction);
                samples.Add(new Vector3(location.x, location.y, Mathf.Lerp(previousTime, now, fraction)));
                distanceSinceEmission = timeSinceEmission = 0f;
                if (fraction >= .999999f) break;
            }
        }
        active = true;
        previous = point;
        previousTime = now;
    }

    public void Reset()
    {
        active = false;
        distanceSinceEmission = timeSinceEmission = 0f;
    }
}
