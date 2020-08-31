﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Walgelijk
{
    /// <summary>
    /// This object manages a render queue of <see cref="IRenderTask"/>
    /// </summary>
    public sealed class RenderQueue
    {
        private readonly List<Command> commands = new List<Command>();

        /// <summary>
        /// Render the queue by dequeuing and executing each entry
        /// </summary>
        /// <param name="target"></param>
        public void RenderAndReset(RenderTarget target)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].RenderTask.Execute(target);
            }
            commands.Clear();
        }

        /// <summary>
        /// Add a task to the queue. The optional order determines when it's going to be executed. Higher values mean later execution.
        /// </summary>
        public void Add(IRenderTask task, short order = 0)
        {
            var command = new Command { RenderTask = task, Order = order };
            command.Order = order;

            if (commands.Count == 0 || commands.Last().Order <= order)
            {
                commands.Add(command);
                return;
            }

            ReverseSortAdd(command);
        }

        private void ReverseSortAdd(Command command)
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                if (commands[i].Order <= command.Order)
                {
                    commands.Insert(i + 1, command);
                    return;
                }
            }
        }

        /// <summary>
        /// Length of the queue
        /// </summary>
        public int Length => commands.Count;

        private struct Command
        {
            public IRenderTask RenderTask;
            public short Order;
        }
    }
}