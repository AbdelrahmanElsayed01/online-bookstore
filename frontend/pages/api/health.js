// Health check endpoint for the frontend
export default function handler(req, res) {
  if (req.method === 'GET') {
    res.status(200).json({
      status: 'Healthy',
      service: 'Frontend',
      timestamp: new Date().toISOString(),
      version: '1.0.0',
      uptime: process.uptime()
    });
  } else {
    res.setHeader('Allow', ['GET']);
    res.status(405).end(`Method ${req.method} Not Allowed`);
  }
}
