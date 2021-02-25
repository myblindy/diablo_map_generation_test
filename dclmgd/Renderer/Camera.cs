using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Renderer
{
    class Camera
    {
        Vector3 position, target;
        public ref Vector3 Position => ref position;
        public ref Vector3 Target => ref target;
        static readonly Vector3 Up = new(0, 1, 0);
        readonly Action<Matrix4x4> update;

        public void Update() => update(Matrix4x4.CreateLookAt(Position, Target, Up));

        public Camera(Vector3 position, Vector3 target, Action<Matrix4x4> update)
        {
            (Position, Target, this.update) = (position, target, update);
            Update();
        }
    }
}
