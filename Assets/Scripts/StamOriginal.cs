using UnityEngine;
using UnityEngine.UI;

//-----------------------------------------------------------------
// Based on Jos Stam papaer "Real-Time Fluid Dynamics for Games"
// https://pdfs.semanticscholar.org/847f/819a4ea14bd789aca8bc88e85e906cfc657c.pdf
// This is close to original implementation, with only changes to boundaries calculations
public class StamOriginal : MonoBehaviour
{
    //-----------------------------------------------------------------
    public const int            c_Size = 128;
    public const float          c_Diff = 0.00000001f;
    public const float          c_Visc = 0.00000001f;
    //-----------------------------------------------------------------
    // External forces
    private float[,]            m_Sources;

    // Density of the dye in the fluid, to visualize the flow
    private float[,]            m_Density;
    // Previous state of Density of the dye in the fluid
    private float[,]            m_Density0;

    // V - Velocity of the fluid
    private float[,]            m_Vx;
    private float[,]            m_Vy;
    // V0 - Previous state of the Velocity 
    private float[,]            m_Vx0;
    private float[,]            m_Vy0;

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
        m_Sources   = new float[ c_Size + 2, c_Size + 2 ];
        m_Density   = new float[ c_Size + 2, c_Size + 2 ];
        m_Density0  = new float[ c_Size + 2, c_Size + 2 ];
        m_Vx        = new float[ c_Size + 2, c_Size + 2 ];
        m_Vy        = new float[ c_Size + 2, c_Size + 2 ];
        m_Vx0       = new float[ c_Size + 2, c_Size + 2 ];
        m_Vy0       = new float[ c_Size + 2, c_Size + 2 ];

        // Init visualization output
        m_VisTex            = new Texture2D( c_Size, c_Size );
        m_VisImage.texture  = m_VisTex;
        m_VisColors         = m_VisTex.GetPixels();

        // Init external forces 
        m_Sources[ c_Size / 2, c_Size / 3 ] = 22.0f;
        m_Sources[ c_Size / 3, c_Size / 2 ] = 18.0f;

        // Randomize velocities for each cell
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                m_Vx[ i, j ] = Random.Range( -1.0f, 1.0f );
                m_Vy[ i, j ] = Random.Range( -1.0f, 1.0f );
            }
    }
    //-----------------------------------------------------------------
    private void Update()
    {
        //------
        // Velocity Step - move velocities across the volume
        AddExternalSources( m_Vx, m_Sources );
        AddExternalSources( m_Vy, m_Sources );

        (m_Vx, m_Vx0) = (m_Vx0, m_Vx);
        Diffuse( m_Vx, m_Vx0, c_Visc );
        (m_Vy, m_Vy0) = (m_Vy0, m_Vy);
        Diffuse( m_Vy, m_Vy0, c_Visc );

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
        Diffuse( m_Density, m_Density0, c_Diff );

        (m_Density, m_Density0) = (m_Density0, m_Density);
        Advect( m_Density, m_Density0, m_Vx, m_Vy );

        //------
        Visualize();
    }
    //-----------------------------------------------------------------
    public void AddExternalSources( float[,] field, float[,] source )
    {
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
                field[ i, j ] += Time.deltaTime * source[ i, j ];
    }
    //-----------------------------------------------------------------
    // Linear Diffusion solver
    public void LinSolver( float[,] x, float[,] x0, float a, float c )
    {
        // Make several itteration during diffusion to make it smooth
        for( int it = 0; it < m_IterAmount; it++ )
        {
            for( int i = 1; i <= c_Size; i++ )
                for( int j = 1; j <= c_Size; j++ )
                {
                    // Derivation explained in the paper, we can't just blur the cell 
                    // because it can lead to cell explostion when is "a > 0.5"
                    x[ i, j ] = 
                        ( x0[ i, j ] + 
                            a * ( x[ i - 1, j ] + x[ i + 1, j ] + x[ i, j - 1 ] + x[ i, j + 1 ] ) 
                        ) / c; 
                }

            UpdateBounds( x );
        }
    }
    //-----------------------------------------------------------------
    // Wrap boundaries, makes whole volume tiled
    public void UpdateBounds( float[,] x )
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
    public void Diffuse( float[,] x, float[,] x0, float diffRate )
    {
        // a - Diffusion amount
        float a = Time.deltaTime * diffRate * c_Size * c_Size;
        // Denominator for LinSolver calculation
        float c = 1 + 4 * a;
        LinSolver( x, x0, a, c );
    }
    //-----------------------------------------------------------------
    // Backtrace through velocity field (u,v) to find next value in the grid cell
    public void Advect( float[,] d, float[,] d0, float[,] u, float[,] v )
    {
        float dt0 = Time.deltaTime * c_Size;

        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                float x = i - dt0 * u[i, j];
                float y = j - dt0 * v[i, j];
                x = Mathf.Max( x, 0.5f );
                y = Mathf.Max( y, 0.5f );
                x = Mathf.Min( x, c_Size + 0.5f );
                y = Mathf.Min( y, c_Size + 0.5f );
                int i0 = (int) x, i1 = i0 + 1;
                int j0 = (int) y, j1 = j0 + 1;

                float s1 = x - i0, s0 = 1 - s1;
                float t1 = y - j0, t0 = 1 - t1;
                d[ i, j ] = s0 * ( t0 * d0[ i0, j0 ] + t1 * d0[ i0, j1 ] ) +
                            s1 * ( t0 * d0[ i1, j0 ] + t1 * d0[ i1, j1 ] );
            }

        UpdateBounds( d );
    }
    //-----------------------------------------------------------------
    // Makes sure mass conservation is preserved across all volume
    public void Project( float[,] u, float[,] v, float[,] p, float[,] div )
    {
        // Creating divirgenece field
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                div[ i, j ] = -0.5f * 
                    ( u[ i + 1, j ] - u[ i - 1, j ] + v[ i, j + 1 ] - v[ i, j - 1 ] ) 
                    / c_Size;
                p[ i, j ] = 0;
            }

        UpdateBounds( div );
        UpdateBounds( p );

        // Smoothing divirgence field
        LinSolver( p, div, a: 1, c: 4 );

        // Normalize speed so there is no divirgence in the field
        for( int i = 1; i <= c_Size; i++ )
            for( int j = 1; j <= c_Size; j++ )
            {
                u[ i, j ] -= 0.5f * c_Size * ( p[ i + 1, j ] - p[ i - 1, j ] );
                v[ i, j ] -= 0.5f * c_Size * ( p[ i, j + 1 ] - p[ i, j - 1 ] );
            }

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
                m_VisColors[ colorIdx ] = new Color( r: m_Density[ i, j ], 
                                                     g: m_Density[ i, j ], 
                                                     b: m_Density[ i, j ] );
                colorIdx++;
            }
        
        m_VisTex.SetPixels( m_VisColors );
        m_VisTex.Apply();
    }
    //-----------------------------------------------------------------
}
