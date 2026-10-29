import { Component, effect, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Auth } from './core/auth';
import { Api } from './core/api';
import { Icon } from './core/icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, Icon],
  templateUrl: './app.html',
})
export class App {
  auth = inject(Auth);
  api = inject(Api);

  constructor() {
    effect(() => {
      if (this.auth.loggedIn()) { this.api.loadProperties(); this.auth.refreshAccount(); }
      else { this.api.pid.set(''); this.api.properties.set([]); }
    });
  }
}
