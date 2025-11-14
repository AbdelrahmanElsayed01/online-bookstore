import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
  vus: 100,
  duration: '30s',
  thresholds: {
    checks: ['rate>0.95'],          // at least 95% of checks must pass
    http_req_failed: ['rate<0.05'], // allow up to 5% request failures
    http_req_duration: ['p(95)<2000'], // 95% of requests must be under 2s
  },
};

export default function () {
  const res = http.get('http://catalog-service:8080/swagger/index.html');
  check(res, {
    'status is 200': (r) => r.status === 200,
  });
  sleep(1);
}
