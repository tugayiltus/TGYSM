import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App';
import './styles/app.scss';
import './shared/i18n';
import { applyTheme } from './shared/ui/theme';

applyTheme();

const rootElement = document.getElementById('root');
if (rootElement === null) {
  throw new Error('Root element #root is missing.');
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
