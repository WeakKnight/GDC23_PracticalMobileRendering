#ifndef _CUBEMAP_UTILITIES_CGINC_
#define _CUBEMAP_UTILITIES_CGINC_

#include "GlobalConfig.cginc"

// @xiaoxinguo
// Copy from https://code.google.com/archive/p/cubemapgen/source/default/source
// TODO: fix up type is never tested


// Edge fixup type (how to perform smoothing near edge region)
#define CP_FIXUP_NONE            0
#define CP_FIXUP_WARP            1
#define CP_FIXUP_STRETCH         2

//used to index cube faces
#define CP_FACE_X_POS 0
#define CP_FACE_X_NEG 1
#define CP_FACE_Y_POS 2
#define CP_FACE_Y_NEG 3
#define CP_FACE_Z_POS 4
#define CP_FACE_Z_NEG 5


//3x2 matrices that map cube map indexing vectors in 3d 
// (after face selection and divide through by the 
//  _ABSOLUTE VALUE_ of the max coord)
// into NVC space
//Note this currently assumes the D3D cube face ordering and orientation
#define CP_UDIR     0
#define CP_VDIR     1
#define CP_FACEAXIS 2

static float3 sgFace2DMapping[6][3] = {
    //XPOS face
    {{ 0,  0, -1},   //u towards negative Z
     { 0, -1,  0},   //v towards negative Y
     {1,  0,  0}},  //pos X axis  
    //XNEG face
     {{0,  0,  1},   //u towards positive Z
      {0, -1,  0},   //v towards negative Y
      {-1,  0,  0}},  //neg X axis       
    //YPOS face
    {{1, 0, 0},     //u towards positive X
     {0, 0, 1},     //v towards positive Z
     {0, 1 , 0}},   //pos Y axis  
    //YNEG face
    {{1, 0, 0},     //u towards positive X
     {0, 0 , -1},   //v towards negative Z
     {0, -1 , 0}},  //neg Y axis  
    //ZPOS face
    {{1, 0, 0},     //u towards positive X
     {0, -1, 0},    //v towards negative Y
     {0, 0,  1}},   //pos Z axis  
    //ZNEG face
    {{-1, 0, 0},    //u towards negative X
     {0, -1, 0},    //v towards negative Y
     {0, 0, -1}},   //neg Z axis  
};

//--------------------------------------------------------------------------------------
// Convert cubemap face texel coordinates and face idx to 3D vector
// note the U and V coords are integer coords and range from 0 to size-1
//  this routine can be used to generate a normalizer cube map
//--------------------------------------------------------------------------------------
float3 TexelCoordToVect(int a_FaceIdx, float a_U, float a_V, int a_Size, int a_FixupType)
{
    float nvcU, nvcV;

    if (a_FixupType == CP_FIXUP_STRETCH && a_Size > 1)
    {
        // Code from Nvtt : http://code.google.com/p/nvidia-texture-tools/source/browse/trunk/src/nvtt/CubeSurface.cpp      
        // transform from [0..res - 1] to [-1 .. 1], match up edges exactly.
        nvcU = (2.0f * (float)a_U / ((float)a_Size - 1.0f) ) - 1.0f;
        nvcV = (2.0f * (float)a_V / ((float)a_Size - 1.0f) ) - 1.0f;
    }
    else
    {
        // Change from original AMD code
        // transform from [0..res - 1] to [- (1 - 1 / res) .. (1 - 1 / res)]
        // + 0.5f is for texel center addressing
        nvcU = (2.0f * ((float)a_U + 0.5f) / (float)a_Size ) - 1.0f;
        nvcV = (2.0f * ((float)a_V + 0.5f) / (float)a_Size ) - 1.0f;
    }

    if (a_FixupType == CP_FIXUP_WARP && a_Size > 1)
    {
        // Code from Nvtt : http://code.google.com/p/nvidia-texture-tools/source/browse/trunk/src/nvtt/CubeSurface.cpp
        float a = pow(float(a_Size), 2.0f) / pow(float(a_Size - 1), 3.0f);
        nvcU = a * pow(nvcU, 3) + nvcU;
        nvcV = a * pow(nvcV, 3) + nvcV;

        // Get current vector
        //generate x,y,z vector (xform 2d NVC coord to 3D vector)
        //U contribution
        float3 tempVec = sgFace2DMapping[a_FaceIdx][CP_UDIR] * nvcU;    
        //V contribution
        tempVec += sgFace2DMapping[a_FaceIdx][CP_VDIR] * nvcV;
        //add face axis
        tempVec += sgFace2DMapping[a_FaceIdx][CP_FACEAXIS];
        //normalize vector
        return normalize(tempVec);
    }
    else
    {
        //generate x,y,z vector (xform 2d NVC coord to 3D vector)
        //U contribution
        float3 tempVec = sgFace2DMapping[a_FaceIdx][CP_UDIR] * nvcU;
        //V contribution
        tempVec += sgFace2DMapping[a_FaceIdx][CP_VDIR] * nvcV;
        //add face axis
        tempVec += sgFace2DMapping[a_FaceIdx][CP_FACEAXIS];
        //normalize vector
        return normalize(tempVec);
    }
}

//--------------------------------------------------------------------------------------
// Convert 3D vector to cubemap face texel coordinates and face idx 
// note the U and V coords are integer coords and range from 0 to size-1
//  this routine can be used to generate a normalizer cube map
//
// returns face IDX and texel coords
//--------------------------------------------------------------------------------------
/*
Mapping Texture Coordinates to Cube Map Faces
Because there are multiple faces, the mapping of texture coordinates to positions on cube map faces
is more complicated than the other texturing targets.  The EXT_texture_cube_map extension is purposefully
designed to be consistent with DirectX 7's cube map arrangement.  This is also consistent with the cube
map arrangement in Pixar's RenderMan package. 
For cube map texturing, the (s,t,r) texture coordinates are treated as a direction vector (rx,ry,rz)
emanating from the center of a cube.  (The q coordinate can be ignored since it merely scales the vector
without affecting the direction.) At texture application time, the interpolated per-fragment (s,t,r)
selects one of the cube map face's 2D mipmap sets based on the largest magnitude coordinate direction 
the major axis direction). The target column in the table below explains how the major axis direction
maps to the 2D image of a particular cube map target. 

major axis 
direction     target                              sc     tc    ma 
----------    ---------------------------------   ---    ---   --- 
+rx          GL_TEXTURE_CUBE_MAP_POSITIVE_X_EXT   -rz    -ry   rx 
-rx          GL_TEXTURE_CUBE_MAP_NEGATIVE_X_EXT   +rz    -ry   rx 
+ry          GL_TEXTURE_CUBE_MAP_POSITIVE_Y_EXT   +rx    +rz   ry 
-ry          GL_TEXTURE_CUBE_MAP_NEGATIVE_Y_EXT   +rx    -rz   ry 
+rz          GL_TEXTURE_CUBE_MAP_POSITIVE_Z_EXT   +rx    -ry   rz 
-rz          GL_TEXTURE_CUBE_MAP_NEGATIVE_Z_EXT   -rx    -ry   rz

Using the sc, tc, and ma determined by the major axis direction as specified in the table above,
an updated (s,t) is calculated as follows 
s   =   ( sc/|ma| + 1 ) / 2 
t   =   ( tc/|ma| + 1 ) / 2
If |ma| is zero or very nearly zero, the results of the above two equations need not be defined
(though the result may not lead to GL interruption or termination).  Once the cube map face's 2D mipmap
set and (s,t) is determined, texture fetching and filtering proceeds like standard OpenGL 2D texturing. 
*/
// Note this method return U and V in range from 0 to size-1
// SL END
void VectToTexelCoord(float3 a_XYZ, int a_Size, out int a_FaceIdx, out int a_U, out int a_V)
{
   float nvcU, nvcV;
   float maxCoord;
   float3 onFaceXYZ;
   int   faceIdx;
   int   u, v;

   //absolute value 3
   float3 absXYZ = abs(a_XYZ);

   if( (absXYZ[0] >= absXYZ[1]) && (absXYZ[0] >= absXYZ[2]) )
   {
      maxCoord = absXYZ[0];

      if(a_XYZ[0] >= 0) //face = XPOS
      {
         faceIdx = CP_FACE_X_POS;            
      }    
      else
      {
         faceIdx = CP_FACE_X_NEG;                    
      }
   }
   else if ( (absXYZ[1] >= absXYZ[0]) && (absXYZ[1] >= absXYZ[2]) )
   {
      maxCoord = absXYZ[1];

      if(a_XYZ[1] >= 0) //face = XPOS
      {
         faceIdx = CP_FACE_Y_POS;            
      }    
      else
      {
         faceIdx = CP_FACE_Y_NEG;                    
      }    
   }
   else  // if( (absXYZ[2] > absXYZ[0]) && (absXYZ[2] > absXYZ[1]) )
   {
      maxCoord = absXYZ[2];

      if(a_XYZ[2] >= 0) //face = XPOS
      {
         faceIdx = CP_FACE_Z_POS;            
      }    
      else
      {
         faceIdx = CP_FACE_Z_NEG;                    
      }    
   }

   //divide through by max coord so face vector lies on cube face
   onFaceXYZ = a_XYZ * (1.0f/maxCoord);
   nvcU = dot(sgFace2DMapping[ faceIdx ][CP_UDIR], onFaceXYZ );
   nvcV = dot(sgFace2DMapping[ faceIdx ][CP_VDIR], onFaceXYZ );

   // SL BEGIN
   // Modify original AMD code to return value from 0 to Size - 1
   u = (int)floor( (a_Size - 1) * 0.5f * (nvcU + 1.0f) );
   v = (int)floor( (a_Size - 1) * 0.5f * (nvcV + 1.0f) );
   // SL END

   a_FaceIdx = faceIdx;
   a_U = u;
   a_V = v;
}

int cubeFaceIndex(half3 vec)
{
    half3 absVec = abs(vec);
    int faceIdx = (vec.z > 0) ? 4 : 5;
    if (absVec.x > absVec.y)
    {
        if (absVec.x > absVec.z)
        {
            faceIdx = (vec.x > 0) ? 0 : 1;
        }
    }
    else
    {
        if (absVec.y > absVec.z)
        {
            faceIdx = (vec.y > 0) ? 2 : 3;
        }
    }
    return faceIdx;
}

#endif
