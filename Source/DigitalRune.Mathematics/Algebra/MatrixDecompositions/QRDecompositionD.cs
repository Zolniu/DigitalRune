// DigitalRune Engine - Copyright (C) DigitalRune GmbH
// This file is subject to the terms and conditions defined in
// file 'LICENSE.TXT', which is part of this source code package.

using System;


namespace DigitalRune.Mathematics.Algebra
{
  /// <summary>
  /// Computes the QR Decomposition of a matrix (double-precision).
  /// </summary>
  /// <remarks>
  /// <para>
  /// For an m x n matrix A with m ≥ n the QR Decomposition computes an orthogonal matrix Q and a 
  /// upper triangular matrix R so that A = Q * R. 
  /// </para>
  /// <para> 
  /// The QR Decomposition always exists, even if the matrix does not have full rank. 
  /// </para>
  /// <para>
  /// Application: The primary use of QR Decomposition is in computing the least squares solution
  /// for non-square sets of linear equations. This will fail if the matrix does not have full rank.
  /// </para>
  /// </remarks>
  // JAMA documentation: If A is m x n, then Q is m x n and R is n x n.
  public class QRDecompositionD
  {
    //--------------------------------------------------------------
    #region Fields
    //--------------------------------------------------------------

    private readonly MatrixD _qr;  // Working storage.
    private MatrixD _matrixQ;
    private MatrixD _matrixR;
    private MatrixD _matrixH;
    private readonly int _m;
    private readonly int _n;
    private readonly double[] _rDiagonal;  // Diagonal of R.
    #endregion


    //--------------------------------------------------------------
    #region Properties
    //--------------------------------------------------------------

    /// <summary>
    /// Gets a value indicating whether the matrix R has full rank (numerically).
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the matrix R has full rank (numerically); otherwise, 
    /// <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// If R has full rank, A has full column rank, i. e. all column vectors are linearly 
    /// independent.
    /// </remarks>
    public bool HasNumericallyFullRank
    {
      get
      {
        for (int j = 0; j < _n; j++)
          if (Numeric.IsZero(_rDiagonal[j]))
            return false;
        return true;
      }
    }


    /// <summary>
    /// Gets the matrix H with the Householder vectors. (This property returns the internal matrix, 
    /// not a copy.)
    /// </summary>
    /// <value>The lower trapezoidal matrix whose columns define the reflections.</value>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
    public MatrixD H
    {
      get
      {
        if (_matrixH != null)
          return _matrixH;

        _matrixH = new MatrixD(_m, _n);
        for (int i = 0; i < _m; i++)
        {
          for (int j = 0; j < _n; j++)
          {
            if (i >= j)
              _matrixH[i, j] = _qr[i, j];
            //else
            //  h[i, j] = 0;
          }
        }
        return _matrixH;
      }
    }


    /// <summary>
    /// Gets the orthogonal matrix Q. (This property returns the internal matrix, not a copy.)
    /// </summary>
    /// <value>The orthogonal matrix Q.</value>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
    public MatrixD Q
    {
      get
      {
        if (_matrixQ != null)
          return _matrixQ;

        _matrixQ = new MatrixD(_m, _n);
        for (int k = _n - 1; k >= 0; k--)
        {
          //for (int i = 0; i < _m; i++)
          //  q[i, k] = 0;

          if (k < _m)
            _matrixQ[k, k] = 1;
          
          for (int j = k; j < _n; j++)
          {
            if (_qr[k, k] != 0)
            {
              double s = 0;
              for (int i = k; i < _m; i++)
                s += _qr[i, k] * _matrixQ[i, j];
              s = -s / _qr[k, k];
              for (int i = k; i < _m; i++)
                _matrixQ[i, j] += s * _qr[i, k];
            }
          }
        }
        return _matrixQ;
      }
    }


    /// <summary>
    /// Gets the upper triangular matrix R. (This property returns the internal matrix, not a copy.)
    /// </summary>
    /// <value>The upper triangular matrix R.</value>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
    public MatrixD R
    {
      get
      {
        if (_matrixR != null)
          return _matrixR;

        _matrixR = new MatrixD(_n, _n);
        for (int i = 0; i < _n; i++)
        {
          for (int j = 0; j < _n; j++)
          {
            if (i < j)
              _matrixR[i, j] = _qr[i, j];
            else if (i == j)
              _matrixR[i, j] = _rDiagonal[i];
            else
              _matrixR[i, j] = 0;
          }
        }
        return _matrixR;
      }
    }
    #endregion


    //--------------------------------------------------------------
    #region Creation and Cleanup
    //--------------------------------------------------------------

    /// <summary>
    /// Creates the QR decomposition of the given matrix.
    /// </summary>
    /// <param name="matrixA">
    /// The matrix A. (Can be rectangular. NumberOfRows must be ≥ NumberOfColumns.)
    /// </param>
    /// <remarks>
    /// The QR decomposition is computed by Householder reflections.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="matrixA"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The number of rows must be greater than or equal to the number of columns.
    /// </exception>
    public QRDecompositionD(MatrixD matrixA)
    {
      if (matrixA == null)
        throw new ArgumentNullException("matrixA");
      if (matrixA.NumberOfRows < matrixA.NumberOfColumns)
        throw new ArgumentException("The number of rows must be greater than or equal to the number of columns.", "matrixA");

      // Initialize.
      _qr = matrixA.Clone();
      _m = matrixA.NumberOfRows;
      _n = matrixA.NumberOfColumns;
      _rDiagonal = new double[_n];

      // Main loop.
      for (int k = 0; k < _n; k++)
      {
        // Compute 2-norm of k-th column without under/overflow.
        double norm = 0;
        for (int i = k; i < _m; i++)
          norm = MathHelper.Hypotenuse(norm, _qr[i, k]);

        if (norm != 0)   // TODO: Maybe a comparison with an epsilon tolerance is required here!?
        {
          // Form k-th Householder vector.
          if (_qr[k, k] < 0)
            norm = -norm;
          for (int i = k; i < _m; i++)
            _qr[i, k] /= norm;
          _qr[k, k] += 1;

          // Apply transformation to remaining columns.
          for (int j = k + 1; j < _n; j++)
          {
            double s = 0;
            for (int i = k; i < _m; i++)
              s += _qr[i, k] * _qr[i, j];
            s = -s / _qr[k, k];
            for (int i = k; i < _m; i++)
              _qr[i, j] += s * _qr[i, k];
          }
        }
        _rDiagonal[k] = -norm;
      }
    }
    #endregion


    //--------------------------------------------------------------
    #region Methods
    //--------------------------------------------------------------

    /// <summary>
    /// Returns the least squares solution for the equation <c>A * X = B</c>.
    /// </summary>
    /// <param name="matrixB">The matrix B with as many rows as A and any number of columns.</param>
    /// <returns>X with the least squares solution.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="matrixB"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The number of rows does not match.
    /// </exception>
    /// <exception cref="MathematicsException">
    /// The matrix A does not have full rank.
    /// </exception>
    public MatrixD SolveLinearEquations(MatrixD matrixB)
    {
      if (matrixB == null)
        throw new ArgumentNullException("matrixB");
      if (matrixB.NumberOfRows != _m)
        throw new ArgumentException("The number of rows does not match.", "matrixB");
      if (HasNumericallyFullRank == false)
        throw new MathematicsException("The matrix does not have full rank.");

      // Copy right hand side
      MatrixD x = matrixB.Clone();

      // Compute Y = transpose(Q)*B
      for (int k = 0; k < _n; k++)
      {
        for (int j = 0; j < matrixB.NumberOfColumns; j++)
        {
          double s = 0;
          for (int i = k; i < _m; i++)
            s += _qr[i, k] * x[i, j];
          s = -s / _qr[k, k];
          for (int i = k; i < _m; i++)
            x[i, j] += s * _qr[i, k];
        }
      }
      // Solve R*X = Y.
      for (int k = _n - 1; k >= 0; k--)
      {
        for (int j = 0; j < matrixB.NumberOfColumns; j++)
          x[k, j] /= _rDiagonal[k];
        for (int i = 0; i < k; i++)
          for (int j = 0; j < matrixB.NumberOfColumns; j++)
            x[i, j] -= x[k, j] * _qr[i, k];
      }
      return x.GetSubmatrix(0, _n - 1, 0, matrixB.NumberOfColumns - 1);
    }
    #endregion
  }
}
