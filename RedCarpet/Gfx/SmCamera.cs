﻿using OpenTK;
using System;
using System.Collections.Generic;
using static RedCarpet.Object;

namespace RedCarpet.Gfx
{
    public class SmCamera
    {
        // Constants
        private readonly Vector3 cameraUp = new Vector3(0.0f, 1.0f, 0.0f);
        private float _pitch;
        private float _yaw;

        public Vector3 cameraFront = new Vector3(0.0f, 0.0f, -1.0f);
        public Vector3 cameraPosition = Vector3.Zero;

        // Position
        public float X
        {
            get
            {
                return cameraPosition.X;
            }
            set
            {
                cameraPosition.X = value;
            }
        }

        public float Y
        {
            get
            {
                return cameraPosition.Y;
            }
            set
            {
                cameraPosition.Y = value;
            }
        }

        public float Z
        {
            get
            {
                return cameraPosition.Z;
            }
            set
            {
                cameraPosition.Z = value;
            }
        }

        // Rotation
        public float pitch
        {
            get
            {
                return _pitch;
            }
            set
            {
                _pitch = ClampAngle(value);
            }
        }

        public float yaw
        {
            get
            {
                return _yaw;
            }
            set
            {
                _yaw = ClampAngle(value);
            }
        }

        public SmCamera()
        {
            X = 0.0f;
            Y = 0.0f;
            Z = 0.0f;
            pitch = 0.0f;
            yaw = 0.0f;
        }

        public Matrix4 CalculateLookAt()
        {
            // Update cameraFront with the current yaw and pitch
            cameraFront.X = (float)Math.Cos(yaw) * (float)Math.Cos(pitch);
            cameraFront.Y = (float)Math.Sin(pitch);
            cameraFront.Z = (float)Math.Sin(yaw) * (float)Math.Cos(pitch);

            // Calculate the LookAt matrix
            Matrix4 matrix = Matrix4.LookAt(cameraPosition, cameraPosition + cameraFront, cameraUp);

            return matrix;
        }

        public Tuple<string, int> castRay(int mouseX, int mouseY, float controlWidth, float controlHeight, Matrix4 projMatrix, Dictionary<string,List<Object.MapObject>> objs)
        {
            Matrix4 lMat = CalculateLookAt();

            Vector3 normDevCoordsRay = new Vector3((2.0f * mouseX) / controlWidth - 1.0f,
                1.0f - (2.0f * mouseY) / controlHeight, -1.0f);

            Vector4 clipRay = new Vector4(normDevCoordsRay, 1.0f);

            Vector4 eyeRay = Vector4.Transform(clipRay, Matrix4.Invert(projMatrix));

            eyeRay = new Vector4(eyeRay.X, eyeRay.Y, -1, 0);

            Vector3 unNormalizedRay = new Vector3(Vector4.Transform(eyeRay, Matrix4.Invert(lMat)).Xyz);

            Vector3 normalizedRay = Vector3.Normalize(unNormalizedRay);

            foreach (string k in objs.Keys)
            {
                for (int i = 0; i < objs[k].Count; i++)
                {
                    if (objs[k][i].boundingBox == null && !objs[k][i].RequiresCustomRendering)
                        continue;

                    int isHit = 0;
                    if (objs[k][i].RequiresCustomRendering)
                    {
                        foreach (SubObjectBoundingBox b in objs[k][i].GetSubObjectMeshes())
                        {
                            isHit = checkHitAxisAlignedBoundingBox(cameraPosition, normalizedRay, b.box, b.Position);
                            if (isHit == 1) break;
                        }
                    }
                    else isHit = checkHitAxisAlignedBoundingBox(cameraPosition, normalizedRay, objs[k][i].boundingBox, objs[k][i].position);


                    if (isHit == 1)
                    {
                        string temp = objs[k][i].unitConfigName;
                        if (temp.StartsWith("Sky") || temp.Contains("View") || temp.Contains("Step"))
                        {
                            isHit = 0;
                        }
                        else
                        {
                            return new Tuple<string, int>(k, i);
                        }
                    }
                }
            }
            return null;
        }

        public int CastRayToSubObjects(int mouseX, int mouseY, float controlWidth, float controlHeight, Matrix4 projMatrix, SubObjectBoundingBox[] objs)
        {
            Matrix4 lMat = CalculateLookAt();

            Vector3 normDevCoordsRay = new Vector3((2.0f * mouseX) / controlWidth - 1.0f,
                1.0f - (2.0f * mouseY) / controlHeight, -1.0f);

            Vector4 clipRay = new Vector4(normDevCoordsRay, 1.0f);

            Vector4 eyeRay = Vector4.Transform(clipRay, Matrix4.Invert(projMatrix));

            eyeRay = new Vector4(eyeRay.X, eyeRay.Y, -1, 0);

            Vector3 unNormalizedRay = new Vector3(Vector4.Transform(eyeRay, Matrix4.Invert(lMat)).Xyz);

            Vector3 normalizedRay = Vector3.Normalize(unNormalizedRay);

            for (int i = 0; i < objs.Length; i++)
            {
                int isHit = checkHitAxisAlignedBoundingBox(cameraPosition, normalizedRay, objs[i].box, objs[i].Position);
                if (isHit == 1) return i;
            }

            return -1;
        }

        public override string ToString()
        {
            return "x = " + X + ", y = " + Y + ", z = " + Z + ", pitch = " + pitch + ", yaw = " + yaw;
        }

        private static float ClampAngle(float angle)
        {
            if (angle < -360.0f)
                angle = -360.0f;
            if (angle > 360.0f)
                angle = 360.0f;

            return angle;
        }

        private int checkHitAxisAlignedBoundingBox(Vector3 eye, Vector3 ray, SmBoundingBox boundingBox, Vector3 position)
        {
            Vector3 dirFrac = new Vector3(1.0f / ray.X, 1.0f / ray.Y, 1.0f / ray.Z);
            Vector3 lowerBound = boundingBox.minimum + position;
            Vector3 upperBound = boundingBox.maximum + position;

            float t1 = (lowerBound.X - eye.X) * dirFrac.X;
            float t2 = (upperBound.X - eye.X) * dirFrac.X;
            float t3 = (lowerBound.Y - eye.Y) * dirFrac.Y;
            float t4 = (upperBound.Y - eye.Y) * dirFrac.Y;
            float t5 = (lowerBound.Z - eye.Z) * dirFrac.Z;
            float t6 = (upperBound.Z - eye.Z) * dirFrac.Z;

            float tmin = Math.Max(Math.Max(Math.Min(t1, t2), Math.Min(t3, t4)), Math.Min(t5, t6));
            float tmax = Math.Min(Math.Min(Math.Max(t1, t2), Math.Max(t3, t4)), Math.Max(t5, t6));

            if (tmax < 0)
                return 0;

            if (tmin > tmax)
                return 0;

            else
                return 1;
        }
    }
}
