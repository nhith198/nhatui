import { Component, ElementRef, Input, OnChanges, inject } from '@angular/core';
import { createElement, Plus, Pencil, Trash2, Save, X, Camera, Upload, LogOut, Wrench, Check,
  Building2, BedDouble, Users, ClipboardList, MessageSquareWarning, UserRoundCog, LayoutDashboard, Receipt, Wallet } from 'lucide';

const icons = { plus: Plus, edit: Pencil, trash: Trash2, save: Save, close: X, camera: Camera, upload: Upload,
  logout: LogOut, wrench: Wrench, check: Check, building: Building2, room: BedDouble, users: Users,
  contract: ClipboardList, complaint: MessageSquareWarning, staff: UserRoundCog, dashboard: LayoutDashboard, invoice: Receipt, finance: Wallet };

@Component({ selector: 'app-icon', template: '', host: { 'aria-hidden': 'true', class: 'icon' } })
export class Icon implements OnChanges {
  private host = inject(ElementRef<HTMLElement>);
  @Input({ required: true }) name!: keyof typeof icons;
  ngOnChanges() { this.host.nativeElement.replaceChildren(createElement(icons[this.name], { width: '18', height: '18', 'stroke-width': '1.8' })); }
}
