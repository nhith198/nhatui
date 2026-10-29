import { Injectable } from '@angular/core';
import Swal from 'sweetalert2';

@Injectable({ providedIn: 'root' })
export class Alerts {
  success(message: string): void {
    void Swal.fire({
      toast: true,
      position: 'top-end',
      icon: 'success',
      title: message,
      showConfirmButton: false,
      timer: 2400,
      timerProgressBar: true,
      customClass: { popup: 'app-alert app-alert-success' },
    });
  }

  confirm(title: string, text: string, confirmText = 'Xác nhận', dangerous = false, target?: HTMLElement): Promise<boolean> {
    return Swal.fire({
      target,
      title,
      text,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: confirmText,
      cancelButtonText: 'Hủy',
      reverseButtons: true,
      focusCancel: true,
      buttonsStyling: false,
      customClass: {
        popup: 'app-alert',
        confirmButton: dangerous ? 'app-alert-danger' : 'app-alert-confirm',
        cancelButton: 'app-alert-cancel',
      },
    }).then(result => result.isConfirmed);
  }
}
