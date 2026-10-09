import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App';
import './styles/app.scss';
import './shared/i18n';
import { initColorMode } from './shared/ui/colorMode';

initColorMode();

const rootElement = document.getElementById('root');
if (rootElement === null) {
  throw new Error('Root element #root is missing.');
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
