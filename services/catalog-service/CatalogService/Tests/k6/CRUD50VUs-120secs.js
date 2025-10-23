import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 50,          // 50 virtual users
  duration: '2m',   // test runs for 2 minutes
};

const BASE_URL = 'http://localhost:5179/api/Books';
const TOKEN = 'eyJhbGciOiJIUzI1NiIsImtpZCI6IktFd28vbEFtbDc1dHMrUmciLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJodHRwczovL2N5ZWNlc2Fndmdnc3htcmZyeXNlLnN1cGFiYXNlLmNvL2F1dGgvdjEiLCJzdWIiOiJkN2ViOWZjYy00Zjg2LTQxNjAtODVhNC04MjUwMmU1YTc4OTgiLCJhdWQiOiJhdXRoZW50aWNhdGVkIiwiZXhwIjoxNzYxMTMzNzYxLCJpYXQiOjE3NjExMzAxNjEsImVtYWlsIjoiYWJkZWxyaG1hbmdhZDE5N0BnbWFpbC5jb20iLCJwaG9uZSI6IiIsImFwcF9tZXRhZGF0YSI6eyJwcm92aWRlciI6ImVtYWlsIiwicHJvdmlkZXJzIjpbImVtYWlsIl19LCJ1c2VyX21ldGFkYXRhIjp7ImVtYWlsX3ZlcmlmaWVkIjp0cnVlfSwicm9sZSI6ImF1dGhlbnRpY2F0ZWQiLCJhYWwiOiJhYWwxIiwiYW1yIjpbeyJtZXRob2QiOiJwYXNzd29yZCIsInRpbWVzdGFtcCI6MTc2MTEzMDE2MX1dLCJzZXNzaW9uX2lkIjoiZDAwZTM1OTMtNjA5OC00ZWMwLThiNjYtZDk1ZjA5Y2VkMzI0IiwiaXNfYW5vbnltb3VzIjpmYWxzZX0.UtDWLOiDp5RATvJ8qhbbQWY60r0rZJ0vl_rWHlOGtNU';

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
    title: `LoadTest Book ${__VU}-${__ITER}`, // unique title
    author: 'Performance Bot',
    year: 2025,
    price: 19.99,
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
      title: `Updated LoadTest Book ${__VU}-${__ITER}`,
      author: 'Performance Bot Updated',
      price: 24.99,
      year: 2025
    });
    const putRes = http.put(`${BASE_URL}/${bookId}`, updateBook, { headers: HEADERS });
    check(putRes, { 'PUT /Books/:id is 204': (r) => r.status === 204 });
  }

  // 4. DELETE the created book
  if (bookId) {
    const delRes = http.del(`${BASE_URL}/${bookId}`, null, { headers: HEADERS });
    check(delRes, { 'DELETE /Books/:id is 200 or 204': (r) => r.status === 200 || r.status === 204 });
  }

  sleep(1); // small pause between iterations
}
