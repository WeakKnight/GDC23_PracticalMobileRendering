import numpy as np
from scipy.sparse import coo_matrix, linalg
#from scipy.special import factorial
from clr_array_convert import asNetArray
from numba import njit, prange

from numba import config, njit, threading_layer


lobe_dirs = np.array([
[0.0000, 0.0000],
[1.5708, 1.5708], [0.0000, 0.0000], [1.5708, 0.0000], 
[1.5708, 1.5708], [0.9553,-2.3562], [3.1416, 2.3562], [0.9553, 0.7854], [2.1863, 2.3562], 
[3.1416, 2.6180], [1.5708,-2.6180], [1.5708, 1.5708], [2.0344,-3.1416], [2.0344,-1.5708], [2.0344,-0.5236], [2.0344, 1.5708],
[1.5708, 0.7854], [1.1832, 0.0000], [1.5708,-3.1416], [1.1832, 0.7854], [3.1416, 0.0000], [1.5708, 1.5708], [1.5708, 0.3927], [2.2845,-1.5708], [0.8571,-3.1416],
[0.0000, 0.0000], [1.5708, 1.5708], [2.1863, 1.5708], [2.1863,-2.7489], [1.5708,-2.3562], [1.5708,-2.7489], [1.5708,-0.7854], [0.6997, 1.5708], [0.6997,-2.3562], [0.9553, 1.5708], [1.5708, 0.0000],
[1.5708, 0.7854], [1.0213,-2.6180], [2.1203,-1.5708], [1.5708,-1.5708], [3.1416, 1.5708], [1.5708, 0.5236], [2.1203, 1.5708], [1.8241, 1.5708], [0.5913,-0.3142], [1.8241,-1.5708], [2.1203,-3.1416], [1.5708, 0.3927], [2.3389,-1.5708],
[1.5708,-0.5236], [2.0719, 2.6180], [0.6928, 1.5708], [1.5708,-1.5708], [3.1416,-0.3927], [0.6928,-1.5708], [1.7989,-3.1416], [2.0053, 1.5708], [1.8518,-3.1416], [2.0053,-1.5708], [0.6928,-2.3562], [2.2040,-1.5708], [0.8755, 0.0000], [2.2040, 1.5708], [0.6928, 2.6180],
], dtype=np.float32)


@njit(nogil=True)
def spherical_dir(theta, phi):
    x = np.sin(theta) * np.cos(phi)
    y = np.sin(theta) * np.sin(phi)
    z = np.cos(theta)
    return x, y, z


@njit(nogil=True)
def spherical_coord(x, y, z):
    norm = np.sqrt(x**2 + y**2 + z**2)
    theta = np.arccos(z/norm)
    phi = np.arctan2(y, x)
    return theta, phi


@njit(nogil=True)
def sh_idx(m, l):
    return l * (l + 1) + m


@njit(nogil=True)
def P(l, m, x):
    # evaluate an Associated Legendre Polynomial P(l,m,x) at x
    pmm = 1.0;
    if (m > 0):
        somx2 = np.sqrt((1.0 - x) * (1.0 + x))
        fact = 1.0
        i = 1
        while i <= m:
            pmm *= (-fact) * somx2
            fact += 2.0
            i += 1

    if (l == m):
        return pmm

    pmmp1 = x * (2.0 * m + 1.0) * pmm

    if (l == m + 1):
        return pmmp1

    pll = 0.0
    ll = m + 2
    while ll <= l:
        pll = ((2.0 * ll - 1.0) * x * pmmp1 - (ll + m - 1.0) * pmm) / (ll - m)
        pmm = pmmp1
        pmmp1 = pll
        ll += 1

    return pll


@njit(nogil=True)
def factorial(n):
    if n <= 0:
        return 1
    else:
        t = 1
        for i in range(1, n + 1):
            t =  t * i
        return t


@njit(nogil=True)
def K(l, m):
    #  renormalisation constant for SH function
    temp = ((2.0 * l + 1.0) * factorial(l - m)) / (4.0 * np.pi * factorial(l + m))
    return np.sqrt(temp)


@njit(nogil=True)
def sph_harm(m, l, theta, phi):
    #  return a point sample of a Spherical Harmonic basis function
    #  l is the band, range [0..N]
    #  m in the range [-l..l]
    #  theta in the range [0..Pi]
    #  phi in the range [0..2*Pi]
    sqrt2 = np.sqrt(2.0)
    if (m == 0):
        return K(l, 0) * P(l, m, np.cos(theta))
    elif (m > 0):
        return sqrt2 * K(l, m) * np.cos(m * phi) * P(l, m, np.cos(theta))
    else:
        return sqrt2 * K(l, -m) * np.sin(-m * phi) * P(l, -m, np.cos(theta))


@njit(nogil=True)
def eq_D_l(l):
    return np.sqrt(4*np.pi/(2*l+1))


@njit(nogil=True)
def eq_Y_l(l):
    matrix_size = 2*l+1
    dirs = lobe_dirs[(l)**2 : (l+1)**2]
    
    Y_l = np.zeros([matrix_size, matrix_size])
    for row in np.arange(0, matrix_size):
        w = dirs[row]
        for column in np.arange(0, matrix_size):
            m = column - l
            Y_l[row][column] = sph_harm(m, l, w[0], w[1])
    return Y_l


@njit(nogil=True)
def eq_Y(N):
    start_band = 0
    matrix_size = N**2 - start_band**2
    Y_l = np.zeros((matrix_size, matrix_size))

    # lobe sharing
    dirs = lobe_dirs[(N-1)**2 : (N)**2]

    for l in np.arange(start_band, N):
        diagonal_matrix_offset = l**2 - start_band**2
        diagonal_matrix_size = 2*l + 1
        for row in np.arange(0, diagonal_matrix_size):
            w = dirs[row]
            for column in np.arange(0, diagonal_matrix_size):
                m = column - l
                Y_l[row+diagonal_matrix_offset][column+diagonal_matrix_offset] = sph_harm(m, l, w[0], w[1])
    return Y_l


@njit(nogil=True)
def eq_Y_R(N, rot):
    start_band = 0
    matrix_size = N**2 - start_band**2
    Y_l = np.zeros((matrix_size, matrix_size))

    # lobe sharing
    dirs = lobe_dirs[(N-1)**2 : (N)**2]

    for l in np.arange(start_band, N):
        diagonal_matrix_offset = l**2 - start_band**2
        diagonal_matrix_size = 2*l + 1
        for row in np.arange(0, diagonal_matrix_size):
            w = dirs[row]
            theta, phi = w[0], w[1]
            x, y, z = spherical_dir(theta, phi)
            xyz = np.dot(rot, np.array([x, y, z]))
            theta, phi = spherical_coord(xyz[0], xyz[1], xyz[2])
            
            for column in np.arange(0, diagonal_matrix_size):
                m = column - l
                Y_l[row+diagonal_matrix_offset][column+diagonal_matrix_offset] = sph_harm(m, l, theta, phi)
    return Y_l


@njit(nogil=True)
def eq_A_l_hat(l):
    A_hat = np.linalg.inv(eq_Y_l(l))
    return A_hat


@njit(nogil=True)
def eq_A_hat(N):
    A_hat = np.linalg.inv(eq_Y(N))
    return A_hat


def print_matrix(m, N):
    for l in np.arange(N):
        offset = l**2
        for row in np.arange(2*l+1):
            linestr = ""
            for col in np.arange(2*l+1):
                linestr += "{:10.6f}".format(m[offset+row][offset+col]) + " "
            print(linestr)


def print_sh_coeffs(band, coeffs):
    for l in np.arange(band):
        linestr = ""
        for m in np.arange(-l, l+1):
            i = sh_idx(m, l)
            linestr += "{:10.6f}".format(coeffs[i]) + " "
        print(linestr)


@njit(nogil=True)
def build_rotate_matrix(w):
    # TODO: keep it same as c# code for comparison, use more elegant solution in future
    nz = w / np.linalg.norm(w)
    oz = np.array([0.0, 0.0, 1.0], dtype=np.float32).astype(np.float32)

    if (nz[2] >= 0.999):
        nx = np.array([1.0, 0.0, 0.0], dtype=np.float32)
        ny = np.array([0.0, 1.0, 0.0], dtype=np.float32)
        nz = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    elif (nz[2] <= -0.999):
        nx = np.array([1.0, 0.0, 0.0], dtype=np.float32)
        ny = np.array([0.0, -1.0, 0.0], dtype=np.float32)
        nz = np.array([0.0, 0.0, -1.0], dtype=np.float32)
    else:
        nx = np.cross(oz, nz)
        ny = np.cross(nz, nx)

        nx = nx / np.linalg.norm(nx)
        ny = ny / np.linalg.norm(ny)

    nx = nx.astype(np.float32)
    ny = ny.astype(np.float32)
    nz = nz.astype(np.float32)
    return np.vstack((nx, ny, nz))


@njit(nogil=True)
def sh_optimal_direction(coeffs):
    return np.array([-coeffs[3], -coeffs[1], coeffs[2]], dtype=np.float32)


@njit(nogil=True)
def sh_rotate(coeffs, band):
    #print("optimal dir ", sh_optimal_direction(coeffs))
    rotation = build_rotate_matrix(sh_optimal_direction(coeffs))

    # project into Rotated Zonal Harmonic Basis
    A_hat = eq_A_hat(band).astype(np.float32)
    Z_hat = A_hat.transpose().dot(coeffs)

    # rotate in RZHB
    Y_R = eq_Y_R(band, rotation).astype(np.float32)
    coeffs_r = Y_R.transpose().dot(Z_hat)
    return coeffs_r;


@njit(nogil=True, parallel=True)
def main(sh_coeffs, sh_band):
    sh_coeffs_prime = np.empty_like(sh_coeffs)

    items_per_loop = 64
    num_items = sh_coeffs.shape[0]
    num_loops = int(np.ceil(num_items / items_per_loop))

    print("num items", num_items)
    print("num loops", num_loops)

    for i_loop in prange(num_loops):
        for i_item in prange(items_per_loop):
            i = i_loop * items_per_loop + i_item
            if (i < num_items):
                sh_coeffs_prime[i] = sh_rotate(sh_coeffs[i], sh_band)

    return sh_coeffs_prime


if __name__ == "main":
    sh_band = _sh_band
    sh_coeffs = np.array(_sh_coeffs).reshape((-1, sh_band * sh_band))

    sh_coeffs_prime = main(sh_coeffs, sh_band)

    _output = asNetArray(sh_coeffs_prime.reshape((-1)).astype(np.float64))

    #print("Threading layer chosen: %s" % threading_layer())
    #main.parallel_diagnostics(level=4)