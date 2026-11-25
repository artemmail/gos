import { Component } from '@angular/core';

@Component({
  selector: 'app-presentation-page',
  templateUrl: './presentation-page.component.html',
  styleUrls: ['./presentation-page.component.css', './product-preview.css']
})
export class PresentationPageComponent {
  scrollToSteps(): void {
    document.getElementById('how-it-works')?.scrollIntoView({
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
    });
  }
}
