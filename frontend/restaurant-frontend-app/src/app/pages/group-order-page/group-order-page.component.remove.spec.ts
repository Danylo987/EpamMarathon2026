// Remove Guest page tests. Run with: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrderPageComponent } from './group-order-page.component';

const caesar = { id: 'dish1', name: 'Caesar Salad', price: 180 };
const SESSIONS: GroupSessionsResponse = {
  participants: [
    {
      id: 'owner',
      type: 'Owner',
      name: 'Yevhenii',
      isSubmitted: true,
      total: 180,
      items: [{ dish: caesar, quantity: 1 }],
    },
    {
      id: 'andrii',
      type: 'Guest',
      name: 'Andrii',
      isSubmitted: true,
      total: 360,
      items: [{ dish: caesar, quantity: 2 }],
    },
    {
      id: 'olena',
      type: 'Guest',
      name: 'Olena',
      isSubmitted: false,
      total: 180,
      items: [{ dish: caesar, quantity: 1 }],
    },
  ],
  total: 720,
};
const INVITE = {
  GroupReservationId: 'grp-1',
  LocationAddress: 'Rustaveli Avenue 48',
  MaxGuestCount: 5,
  StartTime: '2026-10-21T16:00:00Z',
  EndTime: '2026-10-21T17:30:00Z',
} as GuestInvite;

function buttonByText(root: ParentNode, text: string): HTMLButtonElement | undefined {
  return [...root.querySelectorAll('button')].find((b) => b.textContent?.trim() === text) as
    | HTMLButtonElement
    | undefined;
}

describe('Remove Guest', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  let toast: jasmine.SpyObj<ToastService>;

  function create() {
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: toast },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'res-1' }) } },
        },
      ],
    });
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  function removeButtons(element: HTMLElement): HTMLButtonElement[] {
    return [...element.querySelectorAll('button')].filter(
      (b) => b.textContent?.trim() === 'Remove Guest'
    ) as HTMLButtonElement[];
  }

  beforeEach(() => {
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info']);
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$',
      'getSessions$',
      'getOwnerSessionId$',
      'getStatus$',
      'submit$',
      'removeSession$',
    ]);
    service.getGroupInvite$.and.returnValue(of(INVITE));
    service.getSessions$.and.returnValue(of(structuredClone(SESSIONS)));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getStatus$.and.returnValue(
      of({ currentParticipants: 3, maxCapacity: 5, status: 'Open' as const })
    );
  });

  it('shows Remove Guest for each guest but not for the owner', () => {
    expect(removeButtons(create().element).length).toBe(2);
  });

  it('sets the guest name in the Remove button aria-label', () => {
    expect(removeButtons(create().element)[0].getAttribute('aria-label')).toBe('Remove Andrii');
  });

  it('applies destructive styling to the Remove Guest button', () => {
    expect(removeButtons(create().element)[0].classList).toContain('remove-guest-btn');
  });

  it('opens a named confirmation dialog and updates the list after removal', () => {
    service.removeSession$.and.returnValue(
      of({ participants: SESSIONS.participants.slice(0, 2), total: 540 })
    );
    const { fixture, element } = create();
    const component = fixture.componentInstance;
    const participantCountSpy = jasmine.createSpy('participantCountChanged');
    component.participantsChanged.subscribe(participantCountSpy);

    removeButtons(element)[1].click(); // Olena
    fixture.detectChanges();
    const dialog = element.querySelector('[role="dialog"]')!;
    expect(dialog.textContent).toContain('Are you sure you want to remove');
    expect(dialog.textContent).toContain('Olena');

    buttonByText(dialog, 'Remove Guest')?.click();
    fixture.detectChanges();

    expect(service.removeSession$).toHaveBeenCalledOnceWith('grp-1', 'olena');
    expect(element.querySelectorAll('app-participant-card').length).toBe(2);
    expect(participantCountSpy).toHaveBeenCalledOnceWith(2);
    expect(toast.success).toHaveBeenCalledOnceWith('Guest Removed');
  });

  it('places Cancel to the left of Remove Guest in the dialog', () => {
    const { fixture, element } = create();
    removeButtons(element)[0].click();
    fixture.detectChanges();

    const dialog = element.querySelector('[role="dialog"]')!;
    const actionButtons = [...dialog.querySelectorAll('.dialog-actions button')];
    expect(actionButtons[0].textContent?.trim()).toBe('Cancel');
    expect(actionButtons[1].textContent?.trim()).toBe('Remove Guest');
    expect(actionButtons[0].classList).toContain('remove-dialog-cancel-btn');
    expect(actionButtons[1].classList).toContain('remove-confirm-btn');
  });

  it('keeps the list unchanged when the DELETE request fails', () => {
    service.removeSession$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400 }))
    );
    const { fixture, element } = create();
    removeButtons(element)[1].click();
    fixture.detectChanges();
    buttonByText(element.querySelector('[role="dialog"]')!, 'Remove Guest')?.click();
    fixture.detectChanges();
    expect(element.querySelectorAll('app-participant-card').length).toBe(3);
  });

  it('hides Remove Guest buttons after the group is submitted', () => {
    service.getStatus$.and.returnValue(
      of({ currentParticipants: 3, maxCapacity: 5, status: 'Submitted' as const })
    );
    expect(removeButtons(create().element).length).toBe(0);
  });
});
