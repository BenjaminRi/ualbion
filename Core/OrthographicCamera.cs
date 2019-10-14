﻿using System.Collections.Generic;
using System.Numerics;
using UAlbion.Core.Events;

namespace UAlbion.Core
{
    public class OrthographicCamera : Component, ICamera
    {
        static readonly HandlerSet Handlers = new HandlerSet
        (
            H<OrthographicCamera, ScreenCoordinateSelectEvent>((x, e) => x.TransformSelect(e)),
            H<OrthographicCamera, MagnifyEvent>((x, e) =>
            {
                if (x._magnification < 1.0f && e.Delta > 0)
                    x._magnification = 0.0f;

                x._magnification += e.Delta;

                if (x._magnification < 0.5f)
                    x._magnification = 0.5f;
                x.UpdatePerspectiveMatrix();
                x.Raise(new SetCameraMagnificationEvent(x._magnification));
            }),

            // BUG: This event is not received when the screen is resized while a 3D scene is active.
            H<OrthographicCamera, WindowResizedEvent>((x, e) =>
            {
                x.WindowWidth = e.Width;
                x.WindowHeight = e.Height;
                x.UpdatePerspectiveMatrix();
            })
        );

        void TransformSelect(ScreenCoordinateSelectEvent e)
        {
            var totalMatrix = ViewMatrix * ProjectionMatrix;
            var inverse = totalMatrix.Inverse();
            var normalisedScreenPosition = new Vector3(2 * e.Position.X / WindowWidth - 1.0f, -2 * e.Position.Y / WindowHeight + 1.0f, 0.0f);
            var rayOrigin = Vector3.Transform(normalisedScreenPosition + Vector3.UnitZ, inverse);
            var rayDirection = Vector3.Transform(normalisedScreenPosition, inverse) - rayOrigin;
            rayOrigin = new Vector3(rayOrigin.X, rayOrigin.Y, rayOrigin.Z);
            Raise(new WorldCoordinateSelectEvent(rayOrigin, rayDirection, e.RegisterHit));
        }

        Vector3 _position = new Vector3(0, 0, 1);
        Matrix4x4 _viewMatrix;
        Matrix4x4 _projectionMatrix;
        float _magnification = 1.0f;
        public float WindowWidth { get; private set; }
        public float WindowHeight { get; private set; }

        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;
        public Vector3 Position { get => _position; set { _position = value; UpdateViewMatrix(); } }
        public float Magnification { get => _magnification; set { _magnification = value; UpdatePerspectiveMatrix(); } }
        public Vector3 LookDirection { get; } = new Vector3(0, 0, -1f);
        public float FarDistance => 100f;
        public float FieldOfView => 1f;
        public float NearDistance => 0.1f;

        public float AspectRatio => WindowWidth / WindowHeight;

        public OrthographicCamera() : base(Handlers)
        {
            WindowWidth = 1;
            WindowHeight = 1;
            UpdatePerspectiveMatrix();
            UpdateViewMatrix();
        }

        void UpdatePerspectiveMatrix()
        {
            _projectionMatrix = Matrix4x4.Identity;
            _projectionMatrix.M11 = (2.0f * _magnification) / WindowWidth;
            _projectionMatrix.M22 = (-2.0f * _magnification) / WindowHeight;
        }

        void UpdateViewMatrix()
        {
            _viewMatrix = Matrix4x4.Identity;
            _viewMatrix.M41 = -_position.X;
            _viewMatrix.M42 = -_position.Y;
        }

        public CameraInfo GetCameraInfo() => new CameraInfo
        {
            CameraPosition_WorldSpace = _position,
            CameraLookDirection = LookDirection
        };
    }
}