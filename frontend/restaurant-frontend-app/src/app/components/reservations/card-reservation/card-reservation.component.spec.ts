import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import {
  ReservationCustomer,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { GroupOrderPageComponent } from '@app/pages/group-order-page/group-order-page.component';
import { ToastService } from '@app/services/toast/toast.service';
import { of } from 'rxjs';

const RESERVATION: ReservationCustomer = {
  id: 'res-1',
  tableId: 'table-1',
  startTime: new Date('2026-10-21T16:00:00Z'),
  endTime: new Date('2026-10-21T17:30:00Z'),
  guestCount: 2,
  status: ReservationStatusCustomer.Reserved,
  restaurantName: 'Green & Tasty',
  locationAddress: 'Rustaveli Avenue 48',
  waiterId: 'waiter-1',
  groupReservationId: 'group-1',
};

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;
  let groupOrdersService: jasmine.SpyObj<GroupOrdersService>;

  beforeEach(async () => {
    groupOrdersService = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$',
      'getSessions$',
      'getOwnerSessionId$',
      'getStatus$',
      'submit$',
      'removeSession$',
    ]);
    groupOrdersService.getGroupInvite$.and.returnValue(
      of({
        GroupReservationId: 'group-1',
        LocationId: 'location-1',
        RestaurantName: 'Green & Tasty',
        LocationAddress: 'Rustaveli Avenue 48',
        MaxGuestCount: 5,
        StartTime: '2026-10-21T16:00:00Z',
        EndTime: '2026-10-21T17:30:00Z',
        TableNumber: 1,
        InviterName: 'Owner',
      })
    );
    groupOrdersService.getSessions$.and.returnValue(of({ participants: [], total: 0 }));
    groupOrdersService.getOwnerSessionId$.and.returnValue(of('owner-1'));
    groupOrdersService.getStatus$.and.returnValue(
      of({
        currentParticipants: 0,
        maxCapacity: 5,
        status: 'Open',
      })
    );
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: GroupOrdersService, useValue: groupOrdersService },
        {
          provide: ToastService,
          useValue: jasmine.createSpyObj('ToastService', ['success', 'error', 'info']),
        },
        provideRouter([]),
      ],
      imports: [CardReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    // r — обов'язковий input: без нього компонент падає з NG0950
    fixture.componentRef.setInput('r', RESERVATION);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('asks for confirmation before cancelling the reservation', () => {
    const cancelSpy = jasmine.createSpy('cancel');
    component.cancelReservation.subscribe(cancelSpy);

    const button = fixture.nativeElement.querySelector(
      'button[aria-label="Cancel reservation"]'
    ) as HTMLButtonElement;
    expect(button).not.toBeNull();

    button.click();
    fixture.detectChanges();

    expect(cancelSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain(
      'Are you sure you want to cancel this reservation?'
    );

    const keepButton = [...fixture.nativeElement.querySelectorAll('button')].find(
      (element: HTMLButtonElement) => element.textContent?.trim() === 'Keep Reservation'
    ) as HTMLButtonElement | undefined;
    expect(keepButton).toBeDefined();
    keepButton?.click();
    fixture.detectChanges();

    expect(cancelSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('app-modal')).toBeNull();
  });

  it('cancels the reservation only after confirmation', () => {
    const cancelSpy = jasmine.createSpy('cancel');
    component.cancelReservation.subscribe(cancelSpy);
    fixture.nativeElement.querySelector('button[aria-label="Cancel reservation"]').click();
    fixture.detectChanges();

    const confirmButton = [...fixture.nativeElement.querySelectorAll('button')].find(
      (element: HTMLButtonElement) => element.textContent?.trim() === 'Yes, Cancel'
    ) as HTMLButtonElement | undefined;
    expect(confirmButton).toBeDefined();
    confirmButton?.click();

    expect(cancelSpy).toHaveBeenCalledOnceWith('res-1');
  });

  it('keeps visible space between the confirmation buttons', () => {
    fixture.nativeElement.querySelector('button[aria-label="Cancel reservation"]').click();
    fixture.detectChanges();

    const buttons = [
      ...fixture.nativeElement.querySelectorAll('.cancel-reservation-actions button'),
    ] as HTMLButtonElement[];
    expect(buttons.length).toBe(2);
    const gap = buttons[1].getBoundingClientRect().left - buttons[0].getBoundingClientRect().right;
    expect(gap).toBeGreaterThanOrEqual(23.9);
  });

  it('replaces Edit with a 24px pencil icon beside the trash icon', () => {
    const editSpy = jasmine.createSpy('edit');
    component.editReservation.subscribe(editSpy);

    const editButton = fixture.nativeElement.querySelector(
      'button[aria-label="Edit reservation"]'
    ) as HTMLButtonElement;
    const cancelButton = fixture.nativeElement.querySelector(
      'button[aria-label="Cancel reservation"]'
    ) as HTMLButtonElement;

    expect(editButton).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toMatch(/\bEdit\b/);
    expect(cancelButton).not.toBeNull();

    editButton.click();

    expect(editSpy).toHaveBeenCalledOnceWith(RESERVATION);
  });

  it('aligns the trash and pencil icons vertically with the action buttons', () => {
    const trash = fixture.nativeElement.querySelector('.cancel-reservation-icon') as HTMLElement;
    const pencil = fixture.nativeElement.querySelector('.edit-reservation-icon') as HTMLElement;
    const preOrder = [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === 'Pre-order'
    ) as HTMLElement;
    const groupPreOrder = [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === 'Group pre-order'
    ) as HTMLElement;
    const centerY = (element: HTMLElement) => {
      const { top, height } = element.getBoundingClientRect();
      return top + height / 2;
    };

    expect(Math.abs(centerY(trash) - centerY(preOrder))).toBeLessThan(1);
    expect(Math.abs(centerY(pencil) - centerY(groupPreOrder))).toBeLessThan(1);
  });

  it('shows a blue Reserved banner by default', () => {
    fixture.componentRef.setInput('statusEnum', {
      [ReservationStatusCustomer.Reserved]: { class: 'reserved', label: 'Reserved' },
    });
    fixture.detectChanges();

    const banner = fixture.nativeElement.querySelector('.status-banner') as HTMLElement;
    expect(banner.textContent?.trim()).toBe('Reserved');
    expect(banner.classList).toContain('reserved');
  });

  it('shows a green Completed banner after the pre-order is submitted', () => {
    fixture.componentRef.setInput('r', { ...RESERVATION, hasSubmittedPreOrder: true });
    fixture.detectChanges();

    const banner = fixture.nativeElement.querySelector('.status-banner') as HTMLElement;
    expect(banner.textContent?.trim()).toBe('Completed');
    expect(banner.classList).toContain('completed');
  });

  it('updates the displayed guest count when a group participant leaves', () => {
    const groupOrderButton = [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === 'Group pre-order'
    ) as HTMLButtonElement | undefined;
    groupOrderButton?.click();
    fixture.detectChanges();

    const groupOrderPage = fixture.debugElement.query(By.directive(GroupOrderPageComponent))
      .componentInstance as GroupOrderPageComponent;
    groupOrderPage.participantsChanged.emit(1);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('1 Guest');
  });

  it('shows Cancelled in a red badge at the upper-right without the trash icon', () => {
    fixture.componentRef.setInput('r', {
      ...RESERVATION,
      status: ReservationStatusCustomer.Cancelled,
    });
    fixture.componentRef.setInput('statusEnum', {
      [ReservationStatusCustomer.Cancelled]: { class: 'canceled', label: 'Cancelled' },
    });
    fixture.detectChanges();

    const badge = fixture.nativeElement.querySelector('.status-banner') as HTMLElement;
    expect(badge.textContent?.trim()).toBe('Cancelled');
    expect(badge.classList).toContain('canceled');
    expect(fixture.nativeElement.querySelector('.cancel-reservation-icon')).toBeNull();
  });

  it('opens the group pre-order modal for the reservation', () => {
    const button = [...fixture.nativeElement.querySelectorAll('button')].find(
      (element: HTMLButtonElement) => element.textContent?.trim() === 'Group pre-order'
    ) as HTMLButtonElement | undefined;

    expect(button).toBeDefined();
    button?.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-group-order-page')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Group Pre-Order');
    expect(fixture.nativeElement.textContent).toContain('Updated at');
    expect(fixture.nativeElement.textContent).toContain('Refresh');
    expect(groupOrdersService.getGroupInvite$).not.toHaveBeenCalled();
    expect(groupOrdersService.getSessions$).toHaveBeenCalledWith('group-1');
    expect(groupOrdersService.getStatus$).toHaveBeenCalledWith('group-1');
    expect(groupOrdersService.getOwnerSessionId$).toHaveBeenCalledWith('res-1');
  });
});
