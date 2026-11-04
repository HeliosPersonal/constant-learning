import '@testing-library/jest-dom/vitest';
import { afterEach, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import { transferableAbortController } from 'node:util';

afterEach(() => cleanup());
// Request із Node та його AbortSignal мають належати одному середовищу в jsdom.
const controller = transferableAbortController();
globalThis.AbortController = controller.constructor as typeof AbortController;
globalThis.AbortSignal = controller.signal.constructor as typeof AbortSignal;
window.matchMedia = vi.fn().mockImplementation(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
HTMLDialogElement.prototype.showModal = function () { this.open = true; };
HTMLDialogElement.prototype.close = function () { this.open = false; };
window.scrollTo = vi.fn();
