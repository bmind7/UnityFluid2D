using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;

//-----------------------------------------------------------------
// Based on Jos Stam papaer "Real-Time Fluid Dynamics for Games"
// https://pdfs.semanticscholar.org/847f/819a4ea14bd789aca8bc88e85e906cfc657c.pdf
// DOTS version
public class StamDOTS : MonoBehaviour
{
    //-----------------------------------------------------------------
    public const int            c_Size = 128;
    // +2 for boundaries 
    public const int            c_GridSize = (c_Size + 2) * (c_Size + 2);
    public const float          c_Diff = 0.00000001f;
    public const float          c_Visc = 0.00000001f;
    //-----------------------------------------------------------------
    // External forces
    private NativeArray<float>  m_Sources;

    // Density of the dye in the fluid, to visualize the flow
    private NativeArray<float>  m_Density;
    // Previous state of Density of the dye in the fluid
    private NativeArray<float>  m_Density0;

    // V - Velocity of the fluid
    private NativeArray<float>  m_Vx;
    private NativeArray<float>  m_Vy;
    // V0 - Previous state of the Velocity 
    private NativeArray<float>  m_Vx0;
    private NativeArray<float>  m_Vy0;

    // Visualization texture
    private Texture2D           m_VisTex;
    // Array with visualization colors
    private Color[]             m_VisColors;
    //-----------------------------------------------------------------
    [Tooltip("UI component that handles output of the visualization texture")]
    [SerializeField]
    private RawImage            m_VisImage;
    [SerializeField]
    private int                 m_IterAmount = 4;
    //-----------------------------------------------------------------
    private void OnEnable()
    {
        // Init data structures (2 added for bounary cells)
        m_Sources   = new NativeArray<float>( c_GridSize, Allocator.Persistent );
        m_Density   = new NativeArray<float>( c_GridSize, Allocator.Persistent );
        m_Density0  = new NativeArray<float>( c_GridSize, Allocator.Persistent );
        m_Vx        = new NativeArray<float>( c_GridSize, Allocator.Persistent );
        m_Vy        = new NativeArray<float>( c_GridSize, Allocator.Persistent );
        m_Vx0       = new NativeArray<float>( c_GridSize, Allocator.Persistent );
        m_Vy0       = new NativeArray<float>( c_GridSize, Allocator.Persistent );

        // Init visualization output
        m_VisTex            = new Texture2D( c_Size, c_Size );
        m_VisImage.texture  = m_VisTex;
        m_VisColors         = m_VisTex.GetPixels();

        // Init external forces 
        m_Sources[ (c_Size / 2) + (c_Size / 3) * (c_Size + 2) ] = 22.0f;
        m_Sources[ (c_Size / 3) + (c_Size / 2) * (c_Size + 2) ] = 18.0f;

        // Randomize velocities for each cell
        for( int i = 0; i < c_GridSize; i++ )
        {
            m_Vx[ i ] = Random.Range( -1.0f, 1.0f );
            m_Vy[ i ] = Random.Range( -1.0f, 1.0f );
        }
    }
    //-----------------------------------------------------------------
    private void OnDisable()
    {
        m_Sources.Dispose();
        m_Density.Dispose();
        m_Density0.Dispose();
        m_Vx.Dispose();
        m_Vy.Dispose();
        m_Vx0.Dispose();
        m_Vy0.Dispose();
    }
    //-----------------------------------------------------------------
    private void Update()
    {
        //------
        // Velocity Step - move velocities across the volume
        AddExternalSources( m_Vx, m_Sources );
        AddExternalSources( m_Vy, m_Sources );

        // Diffuse velocities
        float a = Time.deltaTime * c_Visc * c_Size * c_Size; //Diffusion amount
        float c = 1 + 4 * a; // Denominator for LinSolver calculation
        (m_Vx, m_Vx0) = (m_Vx0, m_Vx);
        Diffuse( m_Vx, m_Vx0, a, c );
        (m_Vy, m_Vy0) = (m_Vy0, m_Vy);
        Diffuse( m_Vy, m_Vy0, a, c );

        // Project before self-advection, makes results more precise 
        Project( m_Vx, m_Vy, m_Vx0, m_Vy0 );

        (m_Vx, m_Vx0) = (m_Vx0, m_Vx);
        (m_Vy, m_Vy0) = (m_Vy0, m_Vy);
        Advect( m_Vx, m_Vx0, m_Vx0, m_Vy0 );
        Advect( m_Vy, m_Vy0, m_Vx0, m_Vy0 );

        // Final correction of velocity field
        Project( m_Vx, m_Vy, m_Vx0, m_Vy0 );

        //------
        // Density Step - Move densities across the volume
        AddExternalSources( m_Density, m_Sources );

        (m_Density, m_Density0) = (m_Density0, m_Density);
        a = Time.deltaTime * c_Diff * c_Size * c_Size; //Diffusion amount
        c = 1 + 4 * a; // Denominator for LinSolver calculation
        Diffuse( m_Density, m_Density0, a, c );

        (m_Density, m_Density0) = (m_Density0, m_Density);
        Advect( m_Density, m_Density0, m_Vx, m_Vy );

        //------
        Visualize();
    }
    //-----------------------------------------------------------------
    public void AddExternalSources( NativeArray<float> field, NativeArray<float> source )
    {
        var addExternalJob = new AddExternalSourcesPJob()
        {
            sources = source,
            field = field,
            deltaTime = Time.deltaTime
        };
        JobHandle addExternalHandle = addExternalJob.Schedule( c_GridSize, 128 );
        addExternalHandle.Complete();
    }
    //-----------------------------------------------------------------
    // Wrap boundaries, makes whole volume tiled
    public void UpdateBounds( NativeArray<float> x )
    {
        var updateBoundsJob = new UpdateBoundsPJob()
        {
            x       = x,
            size    = c_Size
        };
        JobHandle updateBoundsHandle = updateBoundsJob.Schedule( c_Size + 2, 16 );
        updateBoundsHandle.Complete();
    }
    //-----------------------------------------------------------------
    public void Diffuse( NativeArray<float> x, NativeArray<float> x0, float a, float c)
    {
        // Make several itteration during diffusion to make it smooth
        for( int it = 0; it < m_IterAmount; it++ )
        {
            // Diffuse dye across the volume
            var linSolverJob    = new LinSolverPJob()
            {
                x0      = x0,
                x       = x,
                xC      = new NativeArray<float>( x, Allocator.TempJob ),
                a       = a, // Diffusion amount
                c       = c, // Denominator for LinSolver calculation
                size    = c_Size
            };
            JobHandle linSolverHandle = linSolverJob.Schedule( c_Size + 2, 1 );
            linSolverHandle.Complete();
            linSolverJob.xC.Dispose();

            // Update boundary cells
            UpdateBounds( x );
        }
    }
    //-----------------------------------------------------------------
    // Backtrace through velocity field (u,v) to find next value in the grid cell
    public void Advect( NativeArray<float> d, 
                        NativeArray<float> d0, 
                        NativeArray<float> u, 
                        NativeArray<float> v )
    {
        var advectJob = new AdvectPJob()
        {
            d       = d,
            d0      = d0,
            u       = u,
            v       = v,
            dt0     = Time.deltaTime * c_Size,
            size    = c_Size
        };
        JobHandle advectHandle = advectJob.Schedule( c_Size + 2, 1 );
        advectHandle.Complete();

        UpdateBounds( d );
    }
    //-----------------------------------------------------------------
    // Makes sure mass conservation is preserved across all volume
    public void Project( NativeArray<float> u, 
                         NativeArray<float> v, 
                         NativeArray<float> p, 
                         NativeArray<float> div )
    {
        var divFieldJob = new CreateDivFieldPJob()
        {
            u       = u,
            v       = v,
            p       = p,
            div     = div,
            size    = c_Size
        };
        JobHandle divFieldHandle = divFieldJob.Schedule( c_Size + 2, 1 );
        divFieldHandle.Complete();

        UpdateBounds( div );
        UpdateBounds( p );

        // Smoothing divirgence field
        Diffuse( p, div, a: 1, c: 4 );

        var normalizeFieldJob = new NormalizeFieldPJob()
        {
            u       = u,
            v       = v,
            p       = p,
            size    = c_Size
        };
        JobHandle normalizeFieldHandle = normalizeFieldJob.Schedule( c_Size + 2, 1 );
        normalizeFieldHandle.Complete();

        UpdateBounds( u );
        UpdateBounds( v );
    }
    //-----------------------------------------------------------------
    public void Visualize()
    {
        int colorIdx = 0;
        // Transfer dye to colors array
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                m_VisColors[ colorIdx ] = new Color( r: m_Density[ i + j * ( c_Size + 2 ) ],
                                                     g: m_Density[ i + j * ( c_Size + 2 ) ],
                                                     b: m_Density[ i + j * ( c_Size + 2 ) ] );
                colorIdx++;
            }

        m_VisTex.SetPixels( m_VisColors );
        m_VisTex.Apply();
    }
    //-----------------------------------------------------------------
    private int Idx( int i, int j )
    {
        return i + j * ( c_Size + 2 );
    }
    //-----------------------------------------------------------------
}
