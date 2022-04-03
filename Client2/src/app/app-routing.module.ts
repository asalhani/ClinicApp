import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { InjuryInfoComponent } from './injury-info/injury-info.component';


const routes: Routes = [
   { path: 'home', component: HomeComponent },
   { path: 'injuries', component: InjuryInfoComponent },
   { path: '**', component:  HomeComponent}
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    anchorScrolling: 'enabled'
  })],
  exports: [RouterModule]
})
export class AppRoutingModule { }
