using Cysharp.Threading.Tasks;
using HelloDev.Saving.Core;
using HelloDev.Saving.Interfaces;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Test
{
    /// <summary>
    /// Savable wrapper around a Transform. Captures/restores local position,
    /// rotation, and scale.
    /// </summary>
    public class ExampleSavableMonoBehaviour : SavableMonoBehaviour<ExampleState>
    {
        public override string ModuleId { get; protected set; } = "GameBase";

        public override IUnifiedSaveManager SaveManager => UnifiedSaveManagerBehaviour.Instance.Manager;

        /// <inheritdoc/>
        protected override ExampleState SaveState()
        {
            ExampleState exampleState = new ExampleState
            {
                Position = transform.localPosition,
                Rotation = transform.localRotation,
                Scale = transform.localScale,
                MaterialColor = transform.GetComponentInChildren<Renderer>().material.color
            };
            Logger.LogVerbose("Save", $"Saved state for ExampleSavableMonoBehaviour \nPosition: {exampleState.Position}\n Rotation: {exampleState.Rotation}\n Scale: {exampleState.Scale}\n MaterialColor: {exampleState.MaterialColor}");
            return exampleState;
        }

        /// <inheritdoc/>
        protected override UniTask LoadState(ExampleState state)
        {
            transform.localPosition = state.Position;
            transform.localRotation = state.Rotation;
            transform.localScale = state.Scale;
            transform.GetComponentInChildren<Renderer>().material.color = state.MaterialColor;
            Logger.LogVerbose("Save", $"Loaded state for ExampleSavableMonoBehaviour \nPosition: {state.Position}\nRotation: {state.Rotation}\nScale: {state.Scale}\nMaterialColor: {state.MaterialColor}");
            return UniTask.CompletedTask;
        }

        public void Randomize()
        {
            transform.localPosition = new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f));
            transform.localRotation = Quaternion.Euler(Random.Range(-360f, 360f), Random.Range(-360f, 360f), Random.Range(-360f, 360f));
            transform.localScale = new Vector3(Random.Range(0.5f, 2f), Random.Range(0.5f, 2f), Random.Range(0.5f, 2f));
            transform.GetComponentInChildren<Renderer>().material.color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
        }
    }
}