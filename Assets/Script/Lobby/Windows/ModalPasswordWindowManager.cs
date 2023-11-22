using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Michsky.MUIP;

namespace FPS.Lobby
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ModalPasswordWindowManager : MonoBehaviour
    {
         
        // Resources
        public TMP_InputField windowInput;
        public ButtonManager confirmButton;
        public ButtonManager cancelButton;
        public Animator mwAnimator;

        // Events
        public UnityEvent onOpen;
        public UnityEvent onConfirm;
        public UnityEvent onCancel;

        // Settings
        public bool useCustomContent = false;
        public bool isOn = false;
        public bool closeOnCancel = true;
        public bool closeOnConfirm = true;
        public bool showCancelButton = true;
        public bool showConfirmButton = true;
        public StartBehaviour startBehaviour = StartBehaviour.Disable;
        public CloseBehaviour closeBehaviour = CloseBehaviour.Disable;

        // Helpers
        float cachedStateLength;

        public enum StartBehaviour { None, Disable, Enable }
        public enum CloseBehaviour { None, Disable, Destroy }

        void Awake()
        {
            isOn = false;
            if (mwAnimator == null) { mwAnimator = gameObject.GetComponent<Animator>(); }
            if (closeOnCancel == true) { onCancel.AddListener(CloseWindow); }
            if (confirmButton != null) { confirmButton.onClick.AddListener(onConfirm.Invoke); }
            if (cancelButton != null) { cancelButton.onClick.AddListener(onCancel.Invoke); }
            if (startBehaviour == StartBehaviour.Disable) { isOn = false; gameObject.SetActive(false); }
            else if (startBehaviour == StartBehaviour.Enable) { isOn = false; OpenWindow(); }

            cachedStateLength = MUIPInternalTools.GetAnimatorClipLength(mwAnimator, MUIPInternalTools.modalWindowStateName);
            UpdateUI();
        }

        public void UpdateUI()
        {
            windowInput.text = string.Empty;
            if (useCustomContent == true)
                return;

            if (showCancelButton == true && cancelButton != null) { cancelButton.gameObject.SetActive(true); }
            else if (cancelButton != null) { cancelButton.gameObject.SetActive(false); }

            if (showConfirmButton == true && confirmButton != null) { confirmButton.gameObject.SetActive(true); }
            else if (confirmButton != null) { confirmButton.gameObject.SetActive(false); }
        }

        public void Open()
        {
            if (isOn == false)
            {
                StopCoroutine("DisableObject");
                gameObject.SetActive(true);
                isOn = true;
                onOpen.Invoke();
                mwAnimator.Play("Fade-in");
            }
        }

        public void Close()
        {
            if (isOn == true)
            {
                isOn = false;
                mwAnimator.Play("Fade-out");

                StartCoroutine("DisableObject");
            }
        }

        public string GetInputValue()
        {
            return windowInput.text.Trim();
        }

        // Obsolete
        public void OpenWindow() { Open(); }
        public void CloseWindow() { Close(); }

        public void AnimateWindow()
        {
            if (isOn == false)
            {
                StopCoroutine("DisableObject");

                isOn = true;
                gameObject.SetActive(true);
                mwAnimator.Play("Fade-in");
            }

            else
            {
                isOn = false;
                mwAnimator.Play("Fade-out");

                StartCoroutine("DisableObject");
            }
        }

        IEnumerator DisableObject()
        {
            yield return new WaitForSecondsRealtime(cachedStateLength);

            if (closeBehaviour == CloseBehaviour.Disable) { gameObject.SetActive(false); }
            else if (closeBehaviour == CloseBehaviour.Destroy) { Destroy(gameObject); }
        }
    }
}
