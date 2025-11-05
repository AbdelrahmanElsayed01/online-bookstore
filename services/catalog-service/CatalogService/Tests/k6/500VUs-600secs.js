import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
  vus: 500,
  duration: '600s',
};

export default function () {
  const res = http.get('http://catalog-service:8080/swagger/index.html');
  check(res, {
    'status is 200': (r) => r.status === 200,
  });
  sleep(1);
}