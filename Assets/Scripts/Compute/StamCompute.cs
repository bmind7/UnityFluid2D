using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//-----------------------------------------------------------------
// Based on Jos Stam papaer "Real-Time Fluid Dynamics for Games"
// https://pdfs.semanticscholar.org/847f/819a4ea14bd789aca8bc88e85e906cfc657c.pdf
// Implemented with compute shaders
public class StamCompute : MonoBehaviour
{
    //-----------------------------------------------------------------
    public const int            c_Size = 128;
    public const float          c_Diff = 0.00000001f;
    public const float          c_Visc = 0.00000001f;
    //-----------------------------------------------------------------
    // External forces and sources of dye
    private RenderTexture       m_DyeSources;
    private RenderTexture       m_ForceSources;

    // Density of the dye in the fluid, to visualize the flow
    private RenderTexture       m_Density;
    // Previous state of Density of the dye in the fluid
    private RenderTexture       m_Density0;

    // V - Velocity of the fluid
    private RenderTexture       m_V;
    // V0 - Previous state of the Velocity 
    private RenderTexture       m_V0;

    // Visualization texture
    private RenderTexture       m_VisTex;
    //-----------------------------------------------------------------
    [Tooltip("UI component that handles output of the visualization texture")]
    [SerializeField]
    private RawImage            m_VisImage;
    [SerializeField]
    private int                 m_IterAmount = 4;
    //-----------------------------------------------------------------
    [SerializeField]
    private ComputeShader       m_ModifyFieldCS;
    [SerializeField]
    private ComputeShader       m_AddExternalCS;
    [SerializeField]
    private ComputeShader       m_VisualizeCS;
    [SerializeField]
    private ComputeShader       m_LinSolverCS;
    [SerializeField]
    private ComputeShader       m_UpdateBoundsCS;
    [SerializeField]
    private ComputeShader       m_AdvectCS;
    [SerializeField]
    private ComputeShader       m_ProjectCS;

    //-----------------------------------------------------------------
    private void OnEnable()
    {
        // Init data structures
        m_DyeSources    = new RenderTexture( c_Size, c_Size, 0, RenderTextureFormat.ARGBFloat ) { enableRandomWrite = true };
        m_ForceSources  = new RenderTexture( c_Size, c_Size, 0, RenderTextureFormat.ARGBFloat ) { enableRandomWrite = true };
        m_Density       = new RenderTexture( c_Size, c_Size, 0, RenderTextureFormat.ARGBFloat ) { enableRandomWrite = true };
        m_Density0      = new RenderTexture( c_Size, c_Size, 0, RenderTextureFormat.ARGBFloat ) { enableRandomWrite = true };
        m_V             = new RenderTexture( c_Size, c_Size, 0, RenderTextureFormat.ARGBFloat ) { enableRandomWrite = true };
        m_V0            = new RenderTexture( c_Size, c_Size, 0, RenderTextureFormat.ARGBFloat ) { enableRandomWrite = true };
        m_VisTex        = new RenderTexture( c_Size, c_Size, 0 ) { enableRandomWrite = true };

        m_DyeSources.Create();
        m_ForceSources.Create();
        m_Density.Create();
        m_Density0.Create();
        m_V.Create();
        m_V0.Create();
        m_VisTex.Create();

        // Setup visualization of dye
        m_VisImage.texture = m_VisTex;

        // Prepare compute shader data
        int kernelID = m_ModifyFieldCS.FindKernel( "ModifyField" );
        Vector4[] pixels = new Vector4[ c_Size * c_Size ];
        var computeBuf = new ComputeBuffer(pixels.Length, stride: 4 * 4);

        // Clear dye texture
        computeBuf.SetData( pixels );

        m_ModifyFieldCS.SetBuffer( kernelID, "sources", computeBuf );
        m_ModifyFieldCS.SetTexture( kernelID, "field", m_Density );
        m_ModifyFieldCS.SetInt( "size", c_Size );
        m_ModifyFieldCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );

        // Init external source of dye
        pixels[ c_Size / 2 + (c_Size / 3) * c_Size ] = new Vector3( 50.0f, 0.0f, 50.0f );
        pixels[ c_Size / 3 + (c_Size / 2) * c_Size ] = new Vector3( 0.0f, 40.0f, 40.0f );

        computeBuf.SetData( pixels );

        m_ModifyFieldCS.SetBuffer( kernelID, "sources", computeBuf );
        m_ModifyFieldCS.SetTexture( kernelID, "field", m_DyeSources );
        m_ModifyFieldCS.SetInt( "size", c_Size );
        m_ModifyFieldCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );

        // Init external source of velocity
        pixels[ c_Size / 2 + ( c_Size / 3 ) * c_Size ] = new Vector3( 22.0f, 22.0f, 0.0f );
        pixels[ c_Size / 3 + ( c_Size / 2 ) * c_Size ] = new Vector3( 18.0f, 18.0f, 0.0f );

        computeBuf.SetData( pixels );

        m_ModifyFieldCS.SetBuffer( kernelID, "sources", computeBuf );
        m_ModifyFieldCS.SetTexture( kernelID, "field", m_ForceSources );
        m_ModifyFieldCS.SetInt( "size", c_Size );
        m_ModifyFieldCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );

        // Randomize velocities for each cell
        for( int i = 0; i < pixels.Length; i++ )
            pixels[ i ] = new Vector3( x: Random.Range( -1.0f, 1.0f ), y: Random.Range( -1.0f, 1.0f ), z: 0 );

        computeBuf.SetData( pixels );

        m_ModifyFieldCS.SetBuffer( kernelID, "sources", computeBuf );
        m_ModifyFieldCS.SetTexture( kernelID, "field", m_V );
        m_ModifyFieldCS.SetInt( "size", c_Size );
        m_ModifyFieldCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );

        // Cleanup 
        computeBuf.Dispose();
    }
    //-----------------------------------------------------------------
    private void OnDisable()
    {
        m_DyeSources.Release();
        m_ForceSources.Release();
        m_Density.Release();
        m_Density0.Release();
        m_V.Release();
        m_V0.Release();
        m_VisTex.Release();
    }
    //-----------------------------------------------------------------
    private void Update()
    {
        ////------
        // Velocity Step - move velocities across the volume
        AddExternalSources( m_V, m_ForceSources );

        (m_V, m_V0) = (m_V0, m_V);
        Diffuse( m_V, m_V0, c_Visc );

        // Project before self-advection, makes results more precise 
        Project( m_V, m_V0 );

        (m_V, m_V0) = (m_V0, m_V);
        Advect( m_V, m_V0, m_V0 );

        // Final correction of velocity field
        Project( m_V, m_V0 );

        //------
        // Density Step - Move densities across the volume
        AddExternalSources( m_Density, m_DyeSources );

        (m_Density, m_Density0) = (m_Density0, m_Density);
        Diffuse( m_Density, m_Density0, c_Diff );

        (m_Density, m_Density0) = (m_Density0, m_Density);
        Advect( m_Density, m_Density0, m_V );

        //------
        Visualize();
    }
    //-----------------------------------------------------------------
    public void AddExternalSources( RenderTexture dest, RenderTexture source )
    {
        int kernelID = m_AddExternalCS.FindKernel( "CSMain" );
        m_AddExternalCS.SetTexture( kernelID, "source", source );
        m_AddExternalCS.SetTexture( kernelID, "field", dest );
        m_AddExternalCS.SetFloat( "deltaTime", Time.deltaTime );
        m_AddExternalCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );
    }
    //-----------------------------------------------------------------
    // Linear Diffusion solver
    public void LinSolver( RenderTexture x, RenderTexture x0, float a, float c )
    {
        // Make several itteration during diffusion to make it smooth
        for( int it = 0; it < m_IterAmount; it++ )
        {
            int kernelID = m_LinSolverCS.FindKernel( "CSMain" );
            m_LinSolverCS.SetTexture( kernelID, "x0", x0 );
            m_LinSolverCS.SetTexture( kernelID, "x", x );
            m_LinSolverCS.SetFloat( "a", a );
            m_LinSolverCS.SetFloat( "c", c );
            m_LinSolverCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );

            UpdateBounds( x );
        }
    }
    //-----------------------------------------------------------------
    // Wrap boundaries, makes whole volume tiled
    public void UpdateBounds( RenderTexture x )
    {
        int kernelID = m_UpdateBoundsCS.FindKernel( "CSMain" );
        m_UpdateBoundsCS.SetTexture( kernelID, "x", x );
        m_UpdateBoundsCS.SetInt( "size", c_Size );
        m_UpdateBoundsCS.Dispatch( kernelID, c_Size / 8, 1, 1 );
    }
    //-----------------------------------------------------------------
    public void Diffuse( RenderTexture x, RenderTexture x0, float diffRate )
    {
        // a - Diffusion amount
        float a = Time.deltaTime * diffRate * c_Size * c_Size;
        // Denominator for LinSolver calculation
        float c = 1 + 4 * a;
        LinSolver( x, x0, a, c );
    }
    //-----------------------------------------------------------------
    // Backtrace through velocity field (u,v) to find next value in the grid cell
    public void Advect( RenderTexture d, RenderTexture d0, RenderTexture v )
    {
        int kernelID = m_AdvectCS.FindKernel( "CSMain" );
        m_AdvectCS.SetTexture( kernelID, "d0", d0 );
        m_AdvectCS.SetTexture( kernelID, "d", d );
        m_AdvectCS.SetTexture( kernelID, "v", v );
        m_AdvectCS.SetInt( "size", c_Size );
        m_AdvectCS.SetFloat( "dt0", Time.deltaTime * c_Size );
        m_AdvectCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );


        UpdateBounds( d );
    }
    //-----------------------------------------------------------------
    // Makes sure mass conservation is preserved across all volume
    public void Project( RenderTexture v, RenderTexture div )
    {
        int createDivKernelID = m_ProjectCS.FindKernel( "CreateDivField" );
        m_ProjectCS.SetTexture( createDivKernelID, "vC", v );
        m_ProjectCS.SetTexture( createDivKernelID, "div", div );
        m_ProjectCS.SetInt( "size", c_Size );
        m_ProjectCS.Dispatch( createDivKernelID, c_Size / 8, c_Size / 8, 1 );

        UpdateBounds( div );

        for( int it = 0; it < m_IterAmount; it++ )
        {
            int smoothDivKernelID = m_ProjectCS.FindKernel( "SmoothDivField" );
            m_ProjectCS.SetTexture( smoothDivKernelID, "div", div );
            m_ProjectCS.Dispatch( smoothDivKernelID, c_Size / 8, c_Size / 8, 1 );

            UpdateBounds( div );
        }

        int normFieldKernelID = m_ProjectCS.FindKernel( "NormalizeField" );
        m_ProjectCS.SetTexture( normFieldKernelID, "v", v );
        m_ProjectCS.SetTexture( normFieldKernelID, "div", div );
        m_ProjectCS.SetInt( "size", c_Size );
        m_ProjectCS.Dispatch( normFieldKernelID, c_Size / 8, c_Size / 8, 1 );

        UpdateBounds( v );
    }
    //-----------------------------------------------------------------
    public void Visualize()
    {
        int kernelID = m_VisualizeCS.FindKernel( "CSMain" );
        m_VisualizeCS.SetTexture( kernelID, "source", m_Density );
        m_VisualizeCS.SetTexture( kernelID, "dest", m_VisTex );
        m_VisualizeCS.Dispatch( kernelID, c_Size / 8, c_Size / 8, 1 );
    }
    //-----------------------------------------------------------------
}
