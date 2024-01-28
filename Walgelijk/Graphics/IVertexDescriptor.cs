﻿using System.Collections.Generic;

namespace Walgelijk;

/// <summary>
/// Provides a function that returns all specified attribute arrays of a vertex
/// </summary>
/// <typeparam name="TVertex"></typeparam>
public interface IVertexDescriptor<TVertex>
{
    /// <summary>
    /// Get all attributes of this vertex in the correct order
    /// </summary>
    public IEnumerable<VertexAttributeDescriptor> GetAttributes();
}
