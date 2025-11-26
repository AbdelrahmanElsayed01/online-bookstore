import http from 'k6/http';
import { check, sleep } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export const options = {
  vus: 50,          // 50 virtual users
  duration: '2m',   // test runs for 2 minutes

  thresholds: {
    checks: ['rate>0.95'],          // at least 95% of checks must pass
    http_req_failed: ['rate<0.05'], // allow up to 5% request failures
    http_req_duration: ['p(95)<2000'], // 95% of requests must be under 2s
  },
};

const BASE_URL = 'http://catalog-service:8080/api/books';
const TOKEN = 'eyJhbGciOiJIUzI1NiIsImtpZCI6IktFd28vbEFtbDc1dHMrUmciLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJodHRwczovL2N5ZWNlc2Fndmdnc3htcmZyeXNlLnN1cGFiYXNlLmNvL2F1dGgvdjEiLCJzdWIiOiJkN2ViOWZjYy00Zjg2LTQxNjAtODVhNC04MjUwMmU1YTc4OTgiLCJhdWQiOiJhdXRoZW50aWNhdGVkIiwiZXhwIjoxNzYzOTg5MTkyLCJpYXQiOjE3NjM5ODU1OTIsImVtYWlsIjoiYWJkZWxyaG1hbmdhZDE5N0BnbWFpbC5jb20iLCJwaG9uZSI6IiIsImFwcF9tZXRhZGF0YSI6eyJwcm92aWRlciI6ImVtYWlsIiwicHJvdmlkZXJzIjpbImVtYWlsIl19LCJ1c2VyX21ldGFkYXRhIjp7ImVtYWlsX3ZlcmlmaWVkIjp0cnVlfSwicm9sZSI6ImF1dGhlbnRpY2F0ZWQiLCJhYWwiOiJhYWwxIiwiYW1yIjpbeyJtZXRob2QiOiJwYXNzd29yZCIsInRpbWVzdGFtcCI6MTc2Mzk4NTU5Mn1dLCJzZXNzaW9uX2lkIjoiYmM1MGQ3OTQtZDA4Yy00NjRiLWI1MWItYjU2NWRlYzQxNDEwIiwiaXNfYW5vbnltb3VzIjpmYWxzZX0.8SVWe2isO_6CyYkYohisfofL2nJylpKWYu-1lJ0r2PE';

const HEADERS = {
  Authorization: `Bearer ${TOKEN}`,
  'Content-Type': 'application/json',
};

export default function () {
  // 1. GET all books
  const getRes = http.get(BASE_URL, { headers: HEADERS });
  check(getRes, { 'GET /Books is 200': (r) => r.status === 200 });

  // 2. POST new book
  const newBook = JSON.stringify({
    id: uuidv4(),
    isbn: `ISBN-${__VU}-${__ITER}`,
    book_title: `LoadTest Book ${__VU}-${__ITER}`,
    book_author: 'Performance Bot',
    year_of_publication: 2025,
    publisher: 'LoadTest Publisher',
    image_url_s: 'https://example.com/s.jpg',
    image_url_m: 'https://example.com/m.jpg',
    image_url_l: 'https://example.com/l.jpg',
    stock: 10,
  });

  const postRes = http.post(BASE_URL, newBook, { headers: HEADERS });
  check(postRes, { 'POST /Books is 201': (r) => r.status === 201 || r.status === 200 });

  let bookId;
  try {
    bookId = JSON.parse(postRes.body).id;
  } catch (err) {
    bookId = null;
  }

  // 3. PUT update the created book
  if (bookId) {
    const updateBook = JSON.stringify({
      id: uuidv4(),
      isbn: `ISBN-${__VU}-${__ITER}`,
      book_title: `update LoadTest Book ${__VU}-${__ITER}`,
      book_author: 'update Performance Bot',
      year_of_publication: 2025,
      publisher: 'update LoadTest Publisher',
      image_url_s: 'https://example.com/s.jpg',
      image_url_m: 'https://example.com/m.jpg',
      image_url_l: 'https://example.com/l.jpg',
      stock: 10,
    });

    const putRes = http.put(`${BASE_URL}/${bookId}`, updateBook, { headers: HEADERS });
    check(putRes, { 'PUT /Books/:id is 204': (r) => r.status === 204 });
  }

  // 4. DELETE the created book
  if (bookId) {
    const delRes = http.del(`${BASE_URL}/${bookId}`, null, { headers: HEADERS });
    check(delRes, { 'DELETE /Books/:id is 200 or 204': (r) => r.status === 200 || r.status === 204 });
  }

  sleep(1);
}
