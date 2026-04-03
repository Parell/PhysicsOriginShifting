using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    [System.Serializable]
    public class BodyData
    {
        [HideInInspector] public int index;
        public double mass = 1;
        public float size;
        public Vector3d position;
        public Vector3d velocity;
        public Vector3d acceleration;
        public Vector3 angularVelocity;
        public bool forceKinematic;

        public BodyData() { }

        public BodyData(int index, double mass, Vector3d position, Vector3d velocity, Vector3 angularVelocity, bool forceKinematic)
        {
            this.index = index;
            this.mass = mass;
            this.position = position;
            this.velocity = velocity;
            this.angularVelocity = angularVelocity;
            this.forceKinematic = forceKinematic;
        }
    }

    [System.Serializable]
    public class Keplerian
    {
        public Body parentBody;
        public double a;
        public double e;
        public double w;
        public double lAN;
        public double i;
        public double meanAnomaly;
        public double eAnomaly;
        public double trueAnomaly;
        public double t;
        public double sphereOfInfluence;

        public void CartesianToKeplerian(BodyData body)
        {
            double mu = parentBody.bodyData.mass * Constant.G;
            Vector3d relVelocity = parentBody.bodyData.velocity - body.velocity;
            Vector3d relPosition = parentBody.bodyData.position - body.position;
            Vector3d momentumVector = Vector3d.Cross(relVelocity, relPosition);
            Vector3d eVector = (Vector3d.Cross(momentumVector, relVelocity) / mu) - (relPosition / relPosition.magnitude);
            Vector3d n = new Vector3d(-momentumVector.x, momentumVector.y, 0);

            e = eVector.magnitude;

            if (Vector3d.Dot(relPosition, relVelocity) >= 0)
            {
                trueAnomaly = Mathd.Acos(Vector3d.Dot(eVector, relPosition) / (eVector.magnitude * relPosition.magnitude));
            }
            else
            {
                trueAnomaly = (2 * Mathd.PI) - Mathd.Acos(Vector3d.Dot(eVector, relPosition) / (eVector.magnitude * relPosition.magnitude));
            }

            i = Mathd.Acos(momentumVector.y / momentumVector.magnitude) * (180 / Mathd.PI);

            eAnomaly = 2 * Mathd.Acos(Mathd.Tan(trueAnomaly / 2) / Mathd.Sqrt((1 + eVector.magnitude) / (1 - eVector.magnitude)));

            if (n.x >= 0)
            {
                lAN = Mathd.Acos(n.y / n.magnitude);
            }
            else
            {
                lAN = (2 * Mathd.PI) - Mathd.Acos(n.y / n.magnitude);
            }

            if (eVector.z >= 0)
            {
                w = Mathd.Acos(Vector3d.Dot(n, eVector) / n.magnitude * eVector.magnitude);
            }
            else
            {
                w = (2 * Mathd.PI) - Mathd.Acos(Vector3d.Dot(n, eVector) / n.magnitude * eVector.magnitude);
            }

            meanAnomaly = eAnomaly - (eVector.magnitude * Mathd.Sin(eAnomaly));

            a = 1 / ((2 / relPosition.magnitude) - (Mathd.Pow(relVelocity.magnitude, 2) / mu));

            eAnomaly *= Mathd.Rad2Deg;
            meanAnomaly *= Mathd.Rad2Deg;
            trueAnomaly *= Mathd.Rad2Deg;
            lAN *= Mathd.Rad2Deg;
            w *= Mathd.Rad2Deg;

            sphereOfInfluence = 0.9431f * a * Mathd.Pow(body.mass / parentBody.bodyData.mass, 0.4f);

            t = Mathd.Sqrt(4 * (Mathd.PI * Mathd.PI) / mu * Mathd.Pow(a, 3));
        }
    }
}