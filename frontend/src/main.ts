import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { cfg } from './app/core/models';

// Runtime configuration: the pipeline writes config.json per environment, so one build serves Dev and Prod.
fetch('config.json')
  .then(r => (r.ok ? r.json() : {}))
  .catch(() => ({}))
  .then((c: { apiUrl?: string }) => { if (c.apiUrl) cfg.apiUrl = c.apiUrl; })
  .then(() => bootstrapApplication(App, appConfig))
  .catch(err => console.error(err));
