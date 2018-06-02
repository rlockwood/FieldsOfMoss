using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Automaton : MonoBehaviour {

    public ComputeShader shader;
    public MeshRenderer meshrenderer;

    public int fieldWidth;
    public int fieldHeight;
    public int kernelWidth;
    public int kernelHeight;

    public float drawFill;
    public float drawRadius;

    private ComputeBuffer BufferA;
    private ComputeBuffer BufferB;

    private ComputeBuffer kernel;
    private float[] kernelData;

    private RenderTexture Result;

    private int NoiseFillHandle;
    private int IterateHandle;
    private int RenderHandle;
    private int CursorDrawHandle;

    private bool AtoB;

    private void SetupHandles()
    {
        NoiseFillHandle = shader.FindKernel("NoiseFill");
        IterateHandle = shader.FindKernel("Iterate");
        RenderHandle = shader.FindKernel("Render");
        CursorDrawHandle = shader.FindKernel("CursorDraw");
    }

    private void SetupScalars()
    {
        shader.SetInt("fieldWidth", fieldWidth);
        shader.SetInt("fieldHeight", fieldHeight);

        shader.SetInt("kernelWidth", kernelWidth);
        shader.SetInt("kernelHeight", kernelHeight);
        shader.SetFloat("kernelMult", 1.0f / (float)(kernelWidth * kernelHeight));
    }

    private void SetupBufferA()
    {
        BufferA = new ComputeBuffer(fieldWidth * fieldHeight, sizeof(float));
    }

    private void SetupBufferB()
    {
        BufferB = new ComputeBuffer(fieldWidth * fieldHeight, sizeof(float));
    }

    private void SetupKernel()
    {
        kernel = new ComputeBuffer(kernelWidth * kernelHeight, sizeof(float));

        kernelData = new float[kernelWidth * kernelHeight];

        for(int i = 0; i < kernelWidth; i++)
            for(int j = 0; j < kernelHeight; j++)
            {
                if( (i >= 0 && i <= 2) || (i >= 22 && i <= 24) || (j >= 0 && j <= 2) || (j >= 22 && j <= 24))
                {
                    kernelData[i + j * kernelWidth] = -1.0f;
                }
                else if((i >= 5 && i <= 9) || (i >= 15 && i <= 19) || (j >= 5 && j <= 9) || (j >= 15 && j <= 19))
                {
                    kernelData[i + j * kernelWidth] = 1.0f;
                }
            }

        kernel.SetData(kernelData);

        shader.SetBuffer(IterateHandle, "kernel", kernel);
    }

    private void SetupResult()
    {
        Result = new RenderTexture(fieldWidth, fieldHeight, 24);

        Result.enableRandomWrite = true;
        Result.Create();

        meshrenderer.material.SetTexture("_MainTex", Result);

        shader.SetTexture(RenderHandle, "Result", Result);
    }

    private int IntClamp(int min, int max, int x)
    {
        if (x > max)
            return max;
        else if (x < min)
            return min;
        else
            return x;
    }

    public void Draw(float x, float y)
    {
        shader.SetFloat("cursorRadSqr", drawRadius * drawRadius);
        shader.SetFloat("cursorFill", drawFill);
        shader.SetInt("cursorX", (int)x);
        shader.SetInt("cursorY", (int)y);

        int minX = IntClamp(0, fieldWidth / 32 - 1, (int)(x - drawRadius) / 32);
        int minY = IntClamp(0, fieldHeight / 32 - 1, (int)(y - drawRadius) / 32);
        int maxX = IntClamp(0, fieldWidth / 32 - 1, (int)(x + drawRadius) / 32 + 1);
        int maxY = IntClamp(0, fieldHeight / 32 - 1, (int)(y + drawRadius) / 32 + 1);

        shader.SetInt("drawOffsetX", minX * 32);
        shader.SetInt("drawOffsetY", minY * 32);

        shader.Dispatch(CursorDrawHandle, maxX - minX + 1, maxY - minY + 1, 1);
    }

    // Use this for initialization
    void Start () {
        SetupHandles();
        SetupScalars();

        SetupBufferA();
        SetupBufferB();
        SetupKernel();
        SetupResult();

        AtoB = true;

        //fill buffer in with noise
        shader.SetBuffer(NoiseFillHandle, "fromBuffer", BufferA);
        shader.Dispatch(NoiseFillHandle, fieldWidth / 32, fieldHeight / 32, 1);

        shader.SetBuffer(CursorDrawHandle, "fromBuffer", BufferA);
	}
	
	// Update is called once per frame
	void Update () {
		if(AtoB)
        {
            shader.SetBuffer(IterateHandle, "fromBuffer", BufferA);
            shader.SetBuffer(IterateHandle, "toBuffer", BufferB);
            shader.SetBuffer(RenderHandle, "toBuffer", BufferB);

            shader.SetBuffer(CursorDrawHandle, "fromBuffer", BufferB); //this is set to the "wrong" setting because it's going to be used after we toggle AtoB
        }
        else
        {
            shader.SetBuffer(IterateHandle, "fromBuffer", BufferB);
            shader.SetBuffer(IterateHandle, "toBuffer", BufferA);
            shader.SetBuffer(RenderHandle, "toBuffer", BufferA);

            shader.SetBuffer(CursorDrawHandle, "fromBuffer", BufferA);
        }

        AtoB = !AtoB;

        shader.Dispatch(IterateHandle, fieldWidth / 32, fieldHeight / 32, 1);

        if (Input.GetMouseButton(0))
        {
            //Debug.Log("C");

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100.0f))
            {
                Draw((hit.point.x + 5.0f) * 102.4f, (hit.point.y + 5.0f) * 102.4f);
            }
        }


        shader.Dispatch(RenderHandle, fieldWidth / 32, fieldHeight / 32, 1);
    }

    private void OnApplicationQuit() //be nice and release our buffers (unity will complain otherwise)
    {
        BufferA.Release();
        BufferB.Release();
        kernel.Release();
        Result.Release();
    }
}
