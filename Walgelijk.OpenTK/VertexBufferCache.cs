﻿using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace Walgelijk.OpenTK
{
    public class VertexBufferCache : Cache<VertexBuffer, VertexBufferCacheHandles>
    {
        protected override VertexBufferCacheHandles CreateNew(VertexBuffer buffer)
        {
            int vbo = CreateVertexBufferObject(buffer);
            int vao = CreateVertexArrayObject();
            int ibo = CreateIndexBufferObject(buffer);

            VertexBufferCacheHandles handles = new VertexBufferCacheHandles(vbo, vao, ibo);

            buffer.HasChanged = false;

            return handles;
        }

        private static int CreateIndexBufferObject(VertexBuffer buffer)
        {
            int ibo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(buffer.IndexCount * sizeof(uint)), buffer.Indices, BufferUsageHint.StaticDraw);
            return ibo;
        }

        private static int CreateVertexArrayObject()
        {
            int vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vertex.Stride, sizeof(float) * 0); //position
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Vertex.Stride, sizeof(float) * 3); //texcoords
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, Vertex.Stride, sizeof(float) * 5); //color

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);

            return vao;
        }

        private static int CreateVertexBufferObject(VertexBuffer buffer)
        {
            int vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(buffer.Vertices.Length * Vertex.Stride), buffer.Vertices, BufferUsageHint.StaticDraw);
            return vbo;
        }

        public void UpdateBuffer(VertexBuffer buffer, VertexBufferCacheHandles handles)
        {
            //upload vertices
            GL.BindBuffer(BufferTarget.ArrayBuffer, handles.VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(buffer.Vertices.Length * Vertex.Stride), buffer.Vertices, BufferUsageHint.StaticDraw);

            //upload indices
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, handles.IBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(buffer.IndexCount * sizeof(uint)), buffer.Indices, BufferUsageHint.StaticDraw);

            buffer.HasChanged = false;
        }

        protected override void DisposeOf(VertexBufferCacheHandles loaded)
        {
            throw new NotImplementedException();
        }
    }
}
