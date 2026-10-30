import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
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
      'getGroupInvite$', 'getSessions$', 'getOwnerSessionId$', 'getStatus$', 'submit$', 'removeSession$',
    ]);
    groupOrdersService.getGroupInvite$.and.returnValue(of({
      GroupReservationId: 'group-1',
      LocationId: 'location-1',
      RestaurantName: 'Green & Tasty',
      LocationAddress: 'Rustaveli Avenue 48',
      MaxGuestCount: 5,
      StartTime: '2026-10-21T16:00:00Z',
      EndTime: '2026-10-21T17:30:00Z',
      TableNumber: 1,
      InviterName: 'Owner',
    }));
    groupOrdersService.getSessions$.and.returnValue(of({ participants: [], total: 0 }));
    groupOrdersService.getOwnerSessionId$.and.returnValue(of('owner-1'));
    groupOrdersService.getStatus$.and.returnValue(of({
      currentParticipants: 0,
      maxCapacity: 5,
      status: 'Open',
    }));
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: GroupOrdersService, useValue: groupOrdersService },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error', 'info']) },
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

  it('opens the group pre-order modal for the reservation', () => {
    const button = [...fixture.nativeElement.querySelectorAll('button')]
      .find((element: HTMLButtonElement) => element.textContent?.trim() === 'Group pre-order') as
      | HTMLButtonElement
      | undefined;

    expect(button).toBeDefined();
    button?.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-group-order-page')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Group Pre-Order');
    expect(groupOrdersService.getGroupInvite$).not.toHaveBeenCalled();
    expect(groupOrdersService.getSessions$).toHaveBeenCalledWith('group-1');
    expect(groupOrdersService.getStatus$).toHaveBeenCalledWith('group-1');
    expect(groupOrdersService.getOwnerSessionId$).toHaveBeenCalledWith('res-1');
  });
});
