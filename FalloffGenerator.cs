using UnityEngine;

public static class FalloffGenerator
{
    public static float[,] GenerateFalloffMap(int size)
    {
        float[,] map = new float[size,size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                float x = i/(float)size * 2f - 1f;
                float y = j/(float)size * 2f - 1f;

                float value =Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                map[i,j] = Evaluate(value);
            }
        }

        return map;
    }
    
    private static float Evaluate(float val)
    {
        float a = 3f;
        float b = 2.2f;
        
        return Mathf.Pow(val,a)/(Mathf.Pow(val,a) + Mathf.Pow((b - b * val),a));
    }
}
