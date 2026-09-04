import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import '@/lib/i18n';
import 'bootstrap/dist/css/bootstrap.min.css';
import '@/styles/theme.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Root container is missing from index.html.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
