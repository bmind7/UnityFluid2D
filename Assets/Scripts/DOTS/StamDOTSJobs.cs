using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct AddExternalSourcesPJob : IJobParallelFor
{
    //-----------------------------------------------------------------
    [ReadOnly]
    public NativeArray<float>   sources;
    public NativeArray<float>   field;
    [ReadOnly]
    public float                deltaTime;
    //-----------------------------------------------------------------
    public void Execute( int i )
    {
        field[ i ] = field[ i ] + deltaTime * sources[ i ];
    }
    //-----------------------------------------------------------------
}

public struct UpdateBoundsPJob : IJobParallelFor
{
    //-----------------------------------------------------------------
    [NativeDisableParallelForRestriction]
    public NativeArray<float>   x;
    public int                  size;
    //-----------------------------------------------------------------
    public void Execute( int i )
    {
        if( i == 0 || i == size + 1 )
            return;

        x[ Idx( 0, i ) ]        = x[ Idx( size, i ) ];
        x[ Idx( size + 1, i ) ] = x[ Idx( 1, i ) ];
        x[ Idx( i, 0 ) ]        = x[ Idx( i, size ) ];
        x[ Idx( i, size + 1 ) ] = x[ Idx( i, 1 ) ];
    }
    //-----------------------------------------------------------------
    private int Idx( int i, int j )
    {
        return i + j * ( size + 2 );
    }
    //-----------------------------------------------------------------
}

public struct LinSolverPJob : IJobParallelFor
{
    //-----------------------------------------------------------------
    [ReadOnly, NativeDisableParallelForRestriction]
    public NativeArray<float>   x0;

    [ReadOnly, NativeDisableParallelForRestriction]
    public NativeArray<float>   xC; // Same as `x` but constant

    [NativeDisableParallelForRestriction]
    public NativeArray<float>   x;

    public float                a;
    public float                c;
    public int                  size;
    //-----------------------------------------------------------------
    public void Execute( int j )
    {
        // Skip boundary cells 
        if( j == 0 || j == size + 1 )
            return;

        for( int i = 1; i <= size; i++ )
        {
            // Derivation explained in the paper, we can't just blur the cell 
            // because it can lead to cell explostion when is "a > 0.5"
            x[ Idx( i, j ) ] =
                ( x0[ Idx( i, j ) ] + a * ( xC[ Idx( i - 1, j ) ] + xC[ Idx( i + 1, j ) ] + xC[ Idx( i, j - 1 ) ] + xC[ Idx( i, j + 1 ) ] ) ) / c;
        }

    }
    //-----------------------------------------------------------------
    private int Idx( int i, int j )
    {
        return i + j * ( size + 2 );
    }
    //-----------------------------------------------------------------
}

public struct AdvectPJob : IJobParallelFor
{
    //-----------------------------------------------------------------
    [NativeDisableParallelForRestriction]
    public NativeArray<float>   d;
    [ReadOnly]
    public NativeArray<float>   d0;
    [ReadOnly]
    public NativeArray<float>   u;
    [ReadOnly]
    public NativeArray<float>   v;
    public float                dt0;
    public int                  size;
    //-----------------------------------------------------------------
    public void Execute( int j )
    {
        // Skip boundary cells 
        if( j == 0 || j == size + 1 )
            return;

        for( int i = 1; i <= size; i++ )
        {
            float x = i - dt0 * u[ Idx( i, j ) ];
            float y = j - dt0 * v[ Idx( i, j ) ];
            x = math.max( x, 0.5f );
            y = math.max( y, 0.5f );
            x = math.min( x, size + 0.5f );
            y = math.min( y, size + 0.5f );
            int i0 = (int) x, i1 = i0 + 1;
            int j0 = (int) y, j1 = j0 + 1;

            float s1 = x - i0, s0 = 1 - s1;
            float t1 = y - j0, t0 = 1 - t1;
            d[ Idx( i, j ) ] = s0 * ( t0 * d0[ Idx( i0, j0 ) ] + t1 * d0[ Idx( i0, j1 ) ] ) +
                        s1 * ( t0 * d0[ Idx( i1, j0 ) ] + t1 * d0[ Idx( i1, j1 ) ] );
        }
    }
    //-----------------------------------------------------------------
    private int Idx( int i, int j )
    {
        return i + j * ( size + 2 );
    }
    //-----------------------------------------------------------------
}

public struct CreateDivFieldPJob : IJobParallelFor
{
    //-----------------------------------------------------------------
    [ReadOnly]
    public NativeArray<float>   u;

    [ReadOnly]
    public NativeArray<float>   v;

    [NativeDisableParallelForRestriction]
    public NativeArray<float>   p;

    [NativeDisableParallelForRestriction]
    public NativeArray<float>   div;

    public int                  size;
    //-----------------------------------------------------------------
    public void Execute( int j )
    {
        // Skip boundary cells 
        if( j == 0 || j == size + 1 )
            return;

        for( int i = 1; i <= size; i++ )
        {
            div[ Idx( i, j ) ] = 
                -0.5f * ( u[ Idx( i + 1, j ) ] - u[ Idx( i - 1, j ) ] + v[ Idx( i, j + 1 ) ] - v[ Idx( i, j - 1 ) ] ) / size;
            p[ Idx( i, j ) ] = 0;
        }
    }
    //-----------------------------------------------------------------
    private int Idx( int i, int j )
    {
        return i + j * ( size + 2 );
    }
    //-----------------------------------------------------------------
}

public struct NormalizeFieldPJob : IJobParallelFor
{
    //-----------------------------------------------------------------
    [NativeDisableParallelForRestriction]
    public NativeArray<float>   u;

    [NativeDisableParallelForRestriction]
    public NativeArray<float>   v;

    [ReadOnly]
    public NativeArray<float>   p;

    public int                  size;
    //-----------------------------------------------------------------
    public void Execute( int j )
    {
        // Skip boundary cells 
        if( j == 0 || j == size + 1 )
            return;

        for( int i = 1; i <= size; i++ )
        {
            u[ Idx( i, j ) ] = u[ Idx( i, j ) ] - 0.5f * size * ( p[ Idx( i + 1, j ) ] - p[ Idx( i - 1, j ) ] );
            v[ Idx( i, j ) ] = v[ Idx( i, j ) ] - 0.5f * size * ( p[ Idx( i, j + 1 ) ] - p[ Idx( i, j - 1 ) ] );
        }
    }
    //-----------------------------------------------------------------
    private int Idx( int i, int j )
    {
        return i + j * ( size + 2 );
    }
    //-----------------------------------------------------------------
}

