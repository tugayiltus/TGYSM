/**
 * @input Nothing; runs once from the test setup.
 * @output `showModal`, `show` and `close` on `HTMLDialogElement`, plus Escape as a cancel request.
 * @position Test-only. jsdom has no modal dialog, and Astryx `Dialog` uses the native one.
 */
const ESCAPE = 'Escape';

const openModals: HTMLDialogElement[] = [];

function open(dialog: HTMLDialogElement, modal: boolean) {
  dialog.setAttribute('open', '');
  if (modal) {
    openModals.push(dialog);
  }
}

export function installDialogPolyfill() {
  const proto = window.HTMLDialogElement.prototype;
  if (typeof proto.showModal === 'function') {
    return;
  }
  proto.show = function show(this: HTMLDialogElement) {
    open(this, false);
  };
  proto.showModal = function showModal(this: HTMLDialogElement) {
    open(this, true);
  };
  proto.close = function close(this: HTMLDialogElement, returnValue?: string) {
    if (!this.hasAttribute('open')) {
      return;
    }
    this.removeAttribute('open');
    if (returnValue !== undefined) {
      this.returnValue = returnValue;
    }
    openModals.splice(openModals.indexOf(this) >>> 0, 1);
    this.dispatchEvent(new Event('close'));
  };
  // A browser sends `cancel` to the top modal on Escape, then closes it unless the handler prevents that.
  document.addEventListener('keydown', (event) => {
    const top = openModals.at(-1);
    if (event.key !== ESCAPE || top === undefined || !top.isConnected) {
      return;
    }
    const cancel = new Event('cancel', { cancelable: true });
    if (top.dispatchEvent(cancel)) {
      top.close();
    }
  });
}
