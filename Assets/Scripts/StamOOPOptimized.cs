using UnityEngine;
using UnityEngine.UI;

//-----------------------------------------------------------------
// Based on Jos Stam papaer "Real-Time Fluid Dynamics for Games"
// https://pdfs.semanticscholar.org/847f/819a4ea14bd789aca8bc88e85e906cfc657c.pdf
// OOP version of the original code, some optimization was made to avoid 
// multiple creation of Vector3 strcuts during computations
// Added: support colored dye
public class StamOOPOptimized : MonoBehaviour
{
    //-----------------------------------------------------------------
    public const int            c_Size = 128;
    public const float          c_Diff = 0.00000001f;
    public const float          c_Visc = 0.00000001f;
    //-----------------------------------------------------------------
    // External forces and sources of dye
    private Vector3[,]          m_DyeSources;
    private Vector3[,]          m_ForceSources;

    // Density of the dye in the fluid, to visualize the flow
    private Vector3[,]          m_Density;
    // Previous state of Density of the dye in the fluid
    private Vector3[,]          m_Density0;

    // V - Velocity of the fluid
    private Vector3[,]          m_V;
    // V0 - Previous state of the Velocity 
    private Vector3[,]          m_V0;

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
        m_DyeSources   = new Vector3[ c_Size + 2, c_Size + 2 ];
        m_ForceSources  = new Vector3[ c_Size + 2, c_Size + 2 ];
        m_Density       = new Vector3[ c_Size + 2, c_Size + 2 ];
        m_Density0      = new Vector3[ c_Size + 2, c_Size + 2 ];
        m_V             = new Vector3[ c_Size + 2, c_Size + 2 ];
        m_V0            = new Vector3[ c_Size + 2, c_Size + 2 ];

        // Init visualization output
        m_VisTex            = new Texture2D( c_Size, c_Size );
        m_VisImage.texture  = m_VisTex;
        m_VisColors         = m_VisTex.GetPixels();

        // Init external forces and dye
        m_DyeSources[ c_Size / 2, c_Size / 3 ]      = new Vector3( 50.0f, 0.0f, 50.0f );
        m_DyeSources[ c_Size / 3, c_Size / 2 ]      = new Vector3( 0.0f, 40.0f, 40.0f );
        m_ForceSources[ c_Size / 2, c_Size / 3 ]    = new Vector3( 22.0f, 22.0f, 0f );
        m_ForceSources[ c_Size / 3, c_Size / 2 ]    = new Vector3( 18.0f, 18.0f, 0f );

        // Randomize velocities for each cell
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                m_V[ i, j ] = new Vector3( Random.Range( -1.0f, 1.0f ),
                                           Random.Range( -1.0f, 1.0f ),
                                           0 );
            }
    }
    //-----------------------------------------------------------------
    private void Update()
    {
        //------
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
    public void AddExternalSources( Vector3[,] dest, Vector3[,] source )
    {
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                dest[ i, j ] += Time.deltaTime * source[ i, j ];
            }
    }
    //-----------------------------------------------------------------
    // Linear Diffusion solver
    public void LinSolver( Vector3[,] x, Vector3[,] x0, float a, float c )
    {
        // Make several itteration during diffusion to make it smooth
        for( int it = 0; it < m_IterAmount; it++ )
        {
            for( int i = 1; i <= c_Size; i++ )
                for( int j = 1; j <= c_Size; j++ )
                {
                    // - Derivation explained in the paper, we can't just blur the cell 
                    // because it can lead to cell explostion when is "a > 0.5"
                    // - Constrcut only one Vector3 to avoid ctor invocation during math operations
                    x[ i, j ] = new Vector3(
                        ( x0[ i, j ].x + a * ( x[ i - 1, j ].x + x[ i + 1, j ].x + x[ i, j - 1 ].x + x[ i, j + 1 ].x ) ) / c,
                        ( x0[ i, j ].y + a * ( x[ i - 1, j ].y + x[ i + 1, j ].y + x[ i, j - 1 ].y + x[ i, j + 1 ].y ) ) / c,
                        ( x0[ i, j ].z + a * ( x[ i - 1, j ].z + x[ i + 1, j ].z + x[ i, j - 1 ].z + x[ i, j + 1 ].z ) ) / c
                    );
                }

            UpdateBounds( x );
        }
    }
    //-----------------------------------------------------------------
    // Wrap boundaries, makes whole volume tiled
    public void UpdateBounds( Vector3[,] x )
    {
        for( int i = 1; i <= c_Size; i++ )
        {
            x[ 0, i ] = x[ c_Size, i ];
            x[ c_Size + 1, i ] = x[ 1, i ];
            x[ i, 0 ] = x[ i, c_Size ];
            x[ i, c_Size + 1 ] = x[ i, 1 ];
        }
    }
    //-----------------------------------------------------------------
    public void Diffuse( Vector3[,] x, Vector3[,] x0, float diffRate )
    {
        // a - Diffusion amount
        float a = Time.deltaTime * diffRate * c_Size * c_Size;
        // Denominator for LinSolver calculation
        float c = 1 + 4 * a;
        LinSolver( x, x0, a, c );
    }
    //-----------------------------------------------------------------
    // Backtrace through velocity field (u,v) to find next value in the grid cell
    public void Advect( Vector3[,] d, Vector3[,] d0, Vector3[,] v )
    {
        float dt0 = Time.deltaTime * c_Size;

        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                float x = i - dt0 * v[i, j].x;
                float y = j - dt0 * v[i, j].y;
                x = Mathf.Max( x, 0.5f );
                y = Mathf.Max( y, 0.5f );
                x = Mathf.Min( x, c_Size + 0.5f );
                y = Mathf.Min( y, c_Size + 0.5f );
                int i0 = (int) x, i1 = i0 + 1;
                int j0 = (int) y, j1 = j0 + 1;

                float s1 = x - i0, s0 = 1 - s1;
                float t1 = y - j0, t0 = 1 - t1;
                // Constrcut only one Vector3 to avoid ctor invocation during math operations
                d[ i, j ] = new Vector3(
                        s0 * ( t0 * d0[ i0, j0 ].x + t1 * d0[ i0, j1 ].x ) + s1 * ( t0 * d0[ i1, j0 ].x + t1 * d0[ i1, j1 ].x ),
                        s0 * ( t0 * d0[ i0, j0 ].y + t1 * d0[ i0, j1 ].y ) + s1 * ( t0 * d0[ i1, j0 ].y + t1 * d0[ i1, j1 ].y ),
                        s0 * ( t0 * d0[ i0, j0 ].z + t1 * d0[ i0, j1 ].z ) + s1 * ( t0 * d0[ i1, j0 ].z + t1 * d0[ i1, j1 ].z )
                    );
            }

        UpdateBounds( d );
    }
    //-----------------------------------------------------------------
    // Makes sure mass conservation is preserved across all volume
    public void Project( Vector3[,] v, Vector3[,] div )
    {
        // Creating divirgenece field
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                // Store divirgence field in X component of the vector
                div[ i, j ] = new Vector3(
                    -0.5f * ( v[ i + 1, j ].x - v[ i - 1, j ].x + v[ i, j + 1 ].y - v[ i, j - 1 ].y ) / c_Size,
                    0, 
                    0 );
            }

        UpdateBounds( div );

        // Smooth divirgence field
        for( int it = 0; it < m_IterAmount; it++ )
        {
            for( int i = 1; i <= c_Size; i++ )
                for( int j = 1; j <= c_Size; j++ )
                {
                    // Store smoothed field in Y component of the vector
                    div[ i, j ] = new Vector3(
                        div[ i, j ].x,
                        ( div[ i, j ].x + ( div[ i - 1, j ].y + div[ i + 1, j ].y + div[ i, j - 1 ].y + div[ i, j + 1 ].y ) ) / 4.0f,
                        0 );
                }

            UpdateBounds( div );
        }

        // Normalize speed so there is no divirgence in the field
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                v[ i, j ] = new Vector3(
                    v[ i, j ].x - 0.5f * c_Size * ( div[ i + 1, j ].y - div[ i - 1, j ].y ),
                    v[ i, j ].y - 0.5f * c_Size * ( div[ i, j + 1 ].y - div[ i, j - 1 ].y ),
                    0 );
            }

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
                ref Vector3 dens = ref m_Density[ i, j ];
                m_VisColors[ colorIdx ] = new Color( dens.x, dens.y, dens.z );
                colorIdx++;
            }

        m_VisTex.SetPixels( m_VisColors );
        m_VisTex.Apply();
    }
    //-----------------------------------------------------------------
}
