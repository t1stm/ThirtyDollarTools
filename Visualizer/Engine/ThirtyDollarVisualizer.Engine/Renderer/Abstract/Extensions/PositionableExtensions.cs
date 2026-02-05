using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;

public static class PositionableExtensions
{
    /// <param name="positionable">The positionable object to update.</param>
    /// <typeparam name="T">The type of the object that implements the <see cref="IPositionable" /> interface.</typeparam>
    extension<T>(T positionable) where T : IPositionable
    {
        /// <summary>
        ///     Sets the position of the specified positionable object based on the given position
        ///     and alignment strategy.
        /// </summary>
        /// <param name="position">The target position to be applied to the object.</param>
        /// <param name="positionAlign">
        ///     Specifies the alignment strategy used to determine how the object is positioned
        ///     relative to the target position. The default is a combination of
        ///     <see cref="PositionAlign.Top" /> and <see cref="PositionAlign.Left" />.
        /// </param>
        public void SetPosition(Vector3 position,
            PositionAlign positionAlign = PositionAlign.Top | PositionAlign.Left)
        {
            var scale = positionable.Scale;
            var newPosition = position;

            if (positionAlign.HasFlag(PositionAlign.CenterX))
                newPosition.X -= scale.X / 2;
            if (positionAlign.HasFlag(PositionAlign.CenterY))
                newPosition.Y -= scale.Y / 2;

            if (positionAlign.HasFlag(PositionAlign.Bottom))
                newPosition.Y -= scale.Y;
            if (positionAlign.HasFlag(PositionAlign.Right))
                newPosition.X -= scale.X;

            positionable.Position = newPosition;
        }

        /// <summary>
        ///     Sets the position of the positionable object and returns the updated object.
        /// </summary>
        /// <param name="position">The new position to be applied to the object.</param>
        /// <param name="align">
        ///     Specifies the alignment strategy to be used when setting the position.
        ///     Default is a combination of <see cref="PositionAlign.Top" /> and <see cref="PositionAlign.Left" />.
        /// </param>
        /// <returns>The given updated positionable object.</returns>
        public T WithPosition(Vector3 position,
            PositionAlign align = PositionAlign.Top | PositionAlign.Left)
        {
            positionable.SetPosition(position, align);
            return positionable;
        }
    }
}