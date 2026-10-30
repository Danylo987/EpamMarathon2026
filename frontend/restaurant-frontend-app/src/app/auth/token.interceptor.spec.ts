import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthenticationService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('adds the Owner ID token to the Owner-only group sessions endpoint', () => {
    authService.getTokens.and.returnValue({
      idToken: 'owner-id-token',
      accessToken: 'owner-access-token',
    });

    http.get('https://test.api/guest/groups/group-1/sessions').subscribe();

    const req = httpMock.expectOne('https://test.api/guest/groups/group-1/sessions');
    expect(req.request.headers.get('Authorization')).toBe('Bearer owner-id-token');
    req.flush({ participants: [], total: 0 });
  });

  it('preserves the guest session token on guest session endpoints', () => {
    http
      .get('https://test.api/guest/sessions/guest-1/menu', {
        headers: { Authorization: 'Bearer guest-session-token' },
      })
      .subscribe();

    const req = httpMock.expectOne('https://test.api/guest/sessions/guest-1/menu');
    expect(req.request.headers.get('Authorization')).toBe('Bearer guest-session-token');
    expect(authService.getTokens).not.toHaveBeenCalled();
    req.flush({});
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });

  it('respects SKIP_ERROR_TOAST for guest group status requests', () => {
    let receivedStatus: number | undefined;
    const url = 'https://test.api/guest/groups/group-1/status';
    http
      .get(url, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(url)
      .flush({ message: 'Unavailable' }, { status: 500, statusText: 'Server Error' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(500);
  });
});
