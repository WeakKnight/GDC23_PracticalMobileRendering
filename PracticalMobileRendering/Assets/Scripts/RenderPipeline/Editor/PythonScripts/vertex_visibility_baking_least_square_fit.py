import numpy as np
from scipy.sparse import coo_matrix, linalg
from clr_array_convert import asNetArray

from UnityEngine import Debug
from System.Collections.Generic import List


data = np.array(_data)
row = np.array(_row)
col = np.array(_col)

A = coo_matrix((data, (row, col)), shape=(_row_size, _col_size))
solve = linalg.factorized(A)

B = np.array(_B)
x = np.zeros_like(B)

stride = _row_size
dimensions = _dim

for i in range(dimensions):
    begin, end = i * stride, (i + 1) * stride
    x[begin : end] = solve(B[begin : end])

_X = asNetArray(x)
