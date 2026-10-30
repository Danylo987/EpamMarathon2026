import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import {
  EMPTY,
  catchError,
  distinctUntilChanged,
  exhaustMap,
  finalize,
  forkJoin,
  merge,
  of,
  switchMap,
  tap,
  timer,
} from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import {
  GroupReservationStatus,
  GroupStatusResponse,
} from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

interface GroupOrderHeader {
  GroupReservationId: string;
  LocationAddress: string;
  MaxGuestCount: number;
  StartTime: string | Date;
  EndTime: string | Date;
}

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, CurrencyPipe, DatePipe, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  host: { '[class.modal-mode]': 'modalMode()' },
})
export class GroupOrderPageComponent implements OnInit {
  private static readonly SYNC_INTERVAL_MS = 15_000;

  private readonly route = inject(ActivatedRoute);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly reservationIdInput = input<string | null>(null);
  readonly groupReservationIdInput = input<string | null>(null);
  readonly reservationContext = input<Pick<
    ReservationCustomer,
    'locationAddress' | 'guestCount' | 'startTime' | 'endTime'
  > | null>(null);
  readonly modalMode = input(false);
  private readonly routeReservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  private readonly activeReservationId = computed(
    () => this.reservationIdInput() || this.routeReservationId,
  );
  readonly invite = signal<GroupOrderHeader | null>(null);
  readonly groupReservationId = signal('');
  readonly participants = signal<Participant[]>([]);
  readonly guestSessionId = signal('');
  readonly groupStatus = signal<GroupReservationStatus | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly showSubmitDialog = signal(false);
  readonly submitting = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly refreshError = signal(false);
  readonly refreshing = signal(false);
  readonly selectedGuest = signal<Participant | null>(null);
  readonly removingGuest = signal(false);
  readonly removeError = signal(false);

  private readonly groupStatus$ = toObservable(this.groupStatus).pipe(distinctUntilChanged());

  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));
  readonly grandTotal = computed(() => this.consolidatedOrder().total);
  readonly hasAnyDishes = computed(() =>
    this.participants().some((participant) => participant.items.length > 0)
  );
  readonly unsubmittedCount = computed(
    () => this.participants().filter((participant) => !participant.isSubmitted).length
  );
  readonly isReadOnly = computed(() => this.groupStatus() !== 'Open');
  readonly canSubmit = computed(
    () =>
      this.groupStatus() === 'Open' &&
      this.consolidatedOrder().items.length > 0 &&
      !this.submitting()
  );

  ngOnInit(): void {
    this.startBackgroundSync();
    this.loadGroup();
  }

  retry(): void {
    this.loadGroup();
  }

  openSubmitDialog(): void {
    if (this.canSubmit()) {
      this.showSubmitDialog.set(true);
    }
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) {
      this.showSubmitDialog.set(false);
    }
  }

  refresh(): void {
    if (this.groupStatus() === 'Open' && !this.refreshing()) {
      this.synchronize().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
    }
  }

  openRemoveDialog(participant: Participant): void {
    if (participant.type === 'Guest' && !this.isReadOnly() && !this.removingGuest()) {
      this.selectedGuest.set(participant);
    }
  }

  closeRemoveDialog(): void {
    if (!this.removingGuest()) {
      this.selectedGuest.set(null);
    }
  }

  removeGuest(): void {
    const groupReservationId = this.groupReservationId();
    const guest = this.selectedGuest();
    if (!groupReservationId || !guest || guest.type !== 'Guest' || this.isReadOnly() || this.removingGuest()) {
      return;
    }

    this.removingGuest.set(true);
    this.removeError.set(false);
    this.groupOrdersService
      .removeSession$(groupReservationId, guest.id)
      .pipe(
        finalize(() => this.removingGuest.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (sessions) => {
          this.participants.set(sessions.participants);
          this.selectedGuest.set(null);
          this.updatedAt.set(new Date());
          this.refreshError.set(false);
        },
        error: () => this.removeError.set(true),
      });
  }

  submitPreOrder(): void {
    const groupReservationId = this.groupReservationId();
    if (!groupReservationId || !this.canSubmit()) {
      return;
    }

    this.submitting.set(true);
    this.groupOrdersService
      .submit$(groupReservationId, this.activeReservationId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.groupStatus.set('Submitted');
          this.updatedAt.set(new Date());
          this.submitting.set(false);
          this.showSubmitDialog.set(false);
          this.toast.success('Pre-order sent to the restaurant');
        },
        error: () => {
          this.submitting.set(false);
          this.showSubmitDialog.set(false);
        },
      });
  }

  private loadGroup(): void {
    const groupReservationId = this.groupReservationIdInput();
    if (groupReservationId) {
      this.loadGroupById(groupReservationId);
      return;
    }

    const reservationId = this.activeReservationId();
    if (!reservationId) {
      this.loading.set(false);
      this.loadError.set('Group reservation not found');
      return;
    }

    this.loading.set(true);
    this.loadError.set('');
    this.groupOrdersService
      .getGroupInvite$(reservationId)
      .pipe(
        switchMap((invite) =>
          forkJoin({
            invite: of(invite),
            sessions: this.groupOrdersService.getSessions$(invite.GroupReservationId),
            ownerSessionId: this.groupOrdersService.getOwnerSessionId$(reservationId),
            status: this.groupOrdersService.getStatus$(invite.GroupReservationId),
          })
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ({
          invite,
          sessions,
          ownerSessionId,
          status,
        }: {
          invite: GuestInvite;
          sessions: GroupSessionsResponse;
          ownerSessionId: string;
          status: GroupStatusResponse;
        }) => {
          this.setLoadedGroup(invite, sessions, ownerSessionId, status);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.loadError.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'Group reservation not found'
              : 'Unable to load group pre-order. Please try again.'
          );
        },
      });
  }

  private loadGroupById(groupReservationId: string): void {
    const reservationId = this.activeReservationId();
    const context = this.reservationContext();
    if (!reservationId || !context) {
      this.loading.set(false);
      this.loadError.set('Group reservation not found');
      return;
    }

    this.loading.set(true);
    this.loadError.set('');
    forkJoin({
      sessions: this.groupOrdersService.getSessions$(groupReservationId),
      ownerSessionId: this.groupOrdersService.getOwnerSessionId$(reservationId),
      status: this.groupOrdersService.getStatus$(groupReservationId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ sessions, ownerSessionId, status }) => {
          this.setLoadedGroup(
            {
              GroupReservationId: groupReservationId,
              LocationAddress: context.locationAddress,
              MaxGuestCount: context.guestCount,
              StartTime: context.startTime,
              EndTime: context.endTime,
            },
            sessions,
            ownerSessionId,
            status,
          );
        },
        error: (error: unknown) => this.setLoadError(error),
      });
  }

  private setLoadedGroup(
    header: GroupOrderHeader | GuestInvite,
    sessions: GroupSessionsResponse,
    ownerSessionId: string,
    status: GroupStatusResponse,
  ): void {
    this.invite.set(header);
    this.groupReservationId.set(header.GroupReservationId);
    this.participants.set(sessions.participants);
    this.guestSessionId.set(ownerSessionId);
    this.groupStatus.set(status.status);
    this.updatedAt.set(new Date());
    this.refreshError.set(false);
    this.loading.set(false);
  }

  private setLoadError(error: unknown): void {
    this.loading.set(false);
    this.loadError.set(
      error instanceof HttpErrorResponse && error.status === 404
        ? 'Group reservation not found'
        : 'Unable to load group pre-order. Please try again.',
    );
  }

  private startBackgroundSync(): void {
    this.groupStatus$
      .pipe(
        switchMap((status) =>
          status === 'Open'
            ? timer(
                GroupOrderPageComponent.SYNC_INTERVAL_MS,
                GroupOrderPageComponent.SYNC_INTERVAL_MS,
              ).pipe(exhaustMap(() => this.synchronize()))
            : EMPTY,
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  private synchronize() {
    const groupReservationId = this.groupReservationId();
    if (!groupReservationId || this.refreshing()) {
      return EMPTY;
    }

    this.refreshing.set(true);
    return forkJoin({
      sessions: this.groupOrdersService.getSessions$(groupReservationId, { silent: true }),
      status: this.groupOrdersService.getStatus$(groupReservationId, { silent: true }),
    }).pipe(
      tap(({ sessions, status }) => {
        this.participants.set(sessions.participants);
        this.groupStatus.set(status.status);
        this.updatedAt.set(new Date());
        this.refreshError.set(false);
      }),
      catchError(() => {
        this.refreshError.set(true);
        return EMPTY;
      }),
      finalize(() => this.refreshing.set(false)),
    );
  }
}
