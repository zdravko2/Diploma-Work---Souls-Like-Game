using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways]
public class PuppetStringScript : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;

    public int segmentCount = 10;
    public float segmentLength = 0.2f;
    public float gravity = -9.81f;
    public int iterations = 10;
    public float damping = 0.9f;

    public float maxDeltaTime = 0.033f;
    public float resetThreshold = 0.1f;

    private LineRenderer line;
    private Vector3[] positions;
    private Vector3[] previousPositions;

    private float lastTime;

    void OnEnable()
    {
        Init();
    }

    void OnValidate()
    {
        Init();
    }

    void Init()
    {
        if (startPoint == null) startPoint = transform;
        if (endPoint == null && startPoint.childCount > 0)
            endPoint = startPoint.GetChild(0);

        line = GetComponent<LineRenderer>();
        line.positionCount = segmentCount;

        positions = new Vector3[segmentCount];
        previousPositions = new Vector3[segmentCount];

        Vector3 startPos = startPoint.position;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 pos = startPos + Vector3.down * segmentLength * i;
            positions[i] = pos;
            previousPositions[i] = pos;
        }

        line.SetPositions(positions);

        lastTime = GetTime();
    }

    void Update()
    {
        if (startPoint == null || endPoint == null) return;

        float dt = GetDeltaTime();

        Simulate(dt);
        ApplyConstraints();

        line.SetPositions(positions);
    }

    float GetTime()
    {
        return Time.time;
    }

    float GetDeltaTime()
    {
        float currentTime = GetTime();
        float dt = currentTime - lastTime;
        lastTime = currentTime;
        return dt;
    }

    void Simulate(float dt)
    {
        if (dt > resetThreshold)
        {
            for (int i = 0; i < positions.Length; i++)
                previousPositions[i] = positions[i];

            return;
        }

        dt = Mathf.Min(dt, maxDeltaTime);

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 velocity = (positions[i] - previousPositions[i]) * damping;

            previousPositions[i] = positions[i];
            positions[i] += velocity;
            positions[i] += Vector3.up * gravity * dt * dt;
        }
    }

    void ApplyConstraints()
    {
        for (int i = 0; i < iterations; i++)
        {
            positions[0] = startPoint.position;
            positions[positions.Length - 1] = endPoint.position;

            for (int j = 0; j < positions.Length - 1; j++)
            {
                Vector3 p1 = positions[j];
                Vector3 p2 = positions[j + 1];

                float dist = Vector3.Distance(p1, p2);
                float error = dist - segmentLength;

                Vector3 changeDir = (p1 - p2).normalized;
                Vector3 change = changeDir * error * 0.5f;

                if (j != 0)
                    positions[j] -= change;

                if (j != positions.Length - 2)
                    positions[j + 1] += change;
            }
        }
    }
}